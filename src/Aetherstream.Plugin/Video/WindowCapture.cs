using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Aetherstream.Plugin.Video;

/// <summary>
/// Captures another window's picture with Windows Graphics Capture, the API OBS and Discord use
/// for game capture: it sees a window even when the game is in front of it, and it hands over
/// GPU textures at the display's rate. Spoken to through raw COM vtables rather than the WinRT
/// projections, so nothing beyond Windows itself has to ship; every slot below is derived from
/// the interface's inheritance chain in the comments.
/// <para>
/// A capture runs on its own thread with its own Direct3D device, copies each frame into a
/// staging texture, and keeps the newest one as RGBA for whoever asks. Protected content, a
/// browser playing DRM video say, captures as black by the operating system's design.
/// </para>
/// </summary>
public sealed unsafe class WindowCapture : IDisposable
{
    public readonly record struct WindowInfo(nint Handle, string Title, string Process);

    private const int SlotQueryInterface = 0;
    private const int SlotRelease = 2;

    // IInspectable : IUnknown adds 3 GetIids, 4 GetRuntimeClassName, 5 GetTrustLevel; WinRT
    // interface methods start at 6.
    private const int SlotInteropCreateForWindow = 3;           // IGraphicsCaptureItemInterop : IUnknown
    private const int SlotItemGetSize = 7;                        // IGraphicsCaptureItem: 6 DisplayName, 7 Size
    private const int SlotStaticsCreateFreeThreaded = 6;          // IDirect3D11CaptureFramePoolStatics2
    private const int SlotPoolRecreate = 6;                       // IDirect3D11CaptureFramePool
    private const int SlotPoolTryGetNextFrame = 7;
    private const int SlotPoolCreateCaptureSession = 10;
    private const int SlotFrameGetSurface = 6;                    // IDirect3D11CaptureFrame
    private const int SlotFrameGetContentSize = 8;
    private const int SlotSessionStartCapture = 6;                // IGraphicsCaptureSession
    private const int SlotSession2PutCursor = 7;                  // IGraphicsCaptureSession2: 6 get, 7 put
    private const int SlotSession3PutBorder = 7;                  // IGraphicsCaptureSession3: 6 get, 7 put
    private const int SlotClosableClose = 6;                      // IClosable
    private const int SlotAccessGetInterface = 3;                 // IDirect3DDxgiInterfaceAccess : IUnknown

    // ID3D11Device : IUnknown — 3 CreateBuffer, 4 CreateTexture1D, 5 CreateTexture2D
    private const int SlotDeviceCreateTexture2D = 5;

    // ID3D11Texture2D : ID3D11Resource : ID3D11DeviceChild : IUnknown
    //   3 GetDevice, 4 GetPrivateData, 5 SetPrivateData, 6 SetPrivateDataInterface,
    //   7 GetType, 8 SetEvictionPriority, 9 GetEvictionPriority, 10 GetDesc
    private const int SlotTextureGetDesc = 10;

    // ID3D11DeviceContext: 14 Map, 15 Unmap, ... 46 CopySubresourceRegion, 47 CopyResource
    private const int SlotContextMap = 14;
    private const int SlotContextUnmap = 15;
    private const int SlotContextCopyResource = 47;

    private static readonly Guid IidGraphicsCaptureItemInterop = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    private static readonly Guid IidGraphicsCaptureItem = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid IidFramePoolStatics2 = new("589B103F-6BBC-5DF5-A991-02E28B3B66D5");
    private static readonly Guid IidGraphicsCaptureSession2 = new("2C39AE40-7D2E-5044-804E-8B6799D4CF9E");
    private static readonly Guid IidGraphicsCaptureSession3 = new("F2CDD966-22AE-5EA1-9596-3A289344C3BE");
    private static readonly Guid IidClosable = new("30D5A829-7FA4-4026-83BB-D75BAE4EA99E");
    private static readonly Guid IidDxgiInterfaceAccess = new("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");
    private static readonly Guid IidDxgiDevice = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
    private static readonly Guid IidTexture2D = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");

    private const int PixelFormatB8G8R8A8 = 87;
    private const uint D3D11UsageStaging = 3;
    private const uint D3D11CpuAccessRead = 0x20000;
    private const uint D3D11MapRead = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct SizeInt32
    {
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Texture2DDesc
    {
        public uint Width;
        public uint Height;
        public uint MipLevels;
        public uint ArraySize;
        public uint Format;
        public uint SampleCount;
        public uint SampleQuality;
        public uint Usage;
        public uint BindFlags;
        public uint CpuAccessFlags;
        public uint MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MappedSubresource
    {
        public void* Data;
        public uint RowPitch;
        public uint DepthPitch;
    }

    private readonly object gate = new();
    private Thread? thread;
    private volatile bool running;
    private uint[]? front;
    private uint[]? back;
    private int frontWidth;
    private int frontHeight;
    private long frames;

    /// <summary>The window being captured, or zero.</summary>
    public nint Window { get; private set; }

    /// <summary>Why the capture stopped, when it did not stop on request.</summary>
    public string? Error { get; private set; }

    /// <summary>Frames received so far; zero until the first arrives.</summary>
    public long Frames => Interlocked.Read(ref this.frames);

    /// <summary>Whether this Windows can capture windows at all (Windows 10 1903 or later).</summary>
    public static bool Supported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362);

    // -- windows ---------------------------------------------------------------------------------

    /// <summary>The top-level windows worth offering: visible, titled, not ours, not cloaked.</summary>
    public static List<WindowInfo> ListWindows()
    {
        var list = new List<WindowInfo>();
        var own = Environment.ProcessId;
        var buffer = new StringBuilder(256);

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || GetAncestor(hwnd, 3) != hwnd)
                return true;

            var cloaked = 0;
            if (DwmGetWindowAttribute(hwnd, 14, &cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;

            buffer.Clear();
            if (GetWindowTextW(hwnd, buffer, buffer.Capacity) <= 0)
                return true;
            var title = buffer.ToString().Trim();
            if (title.Length == 0 || title == "Program Manager")
                return true;

            uint pid;
            GetWindowThreadProcessId(hwnd, &pid);
            if (pid == own)
                return true;

            var process = string.Empty;
            try
            {
                process = Process.GetProcessById((int)pid).ProcessName;
            }
            catch
            {
                // Gone, or not ours to ask about.
            }

            list.Add(new WindowInfo(hwnd, title, process));
            return true;
        }, 0);

        return list;
    }

    /// <summary>
    /// Resizes a window so its client area is the given size: a window the same size as the
    /// picture mirrors pixel for pixel. A maximised window is restored first, since a maximised
    /// one ignores a resize. False when the window would not move, a fullscreen game say.
    /// </summary>
    public static bool ResizeClient(nint window, int width, int height)
    {
        if (window == 0)
            return false;
        if (IsZoomed(window))
            ShowWindow(window, 9);

        Rect frame, client;
        if (!GetWindowRect(window, &frame) || !GetClientRect(window, &client))
            return false;
        var extraX = (frame.Right - frame.Left) - (client.Right - client.Left);
        var extraY = (frame.Bottom - frame.Top) - (client.Bottom - client.Top);
        if (!SetWindowPos(window, 0, 0, 0, width + extraX, height + extraY, 0x0002 | 0x0004 | 0x0010))
            return false;

        return GetClientRect(window, &client) && client.Right - client.Left == width && client.Bottom - client.Top == height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(nint hwnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hwnd, int command);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hwnd, Rect* rect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(nint hwnd, Rect* rect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);

    // -- capture ---------------------------------------------------------------------------------

    public void Start(nint window)
    {
        this.Stop();
        this.Window = window;
        this.Error = null;
        this.running = true;
        this.thread = new Thread(() => this.Run(window)) { IsBackground = true, Name = "Aetherstream window capture" };
        this.thread.Start();
    }

    public void Stop()
    {
        this.running = false;
        this.thread?.Join(2000);
        this.thread = null;
        this.Window = 0;
        lock (this.gate)
        {
            this.front = null;
            this.frontWidth = 0;
            this.frontHeight = 0;
        }
    }

    /// <summary>
    /// The newest frame, as RGBA at the window's size. False when none has arrived yet; the
    /// buffer handed back is the caller's to keep until the next call.
    /// </summary>
    public bool TryLatest(out uint[] pixels, out int width, out int height)
    {
        lock (this.gate)
        {
            if (this.front is null)
            {
                pixels = [];
                width = height = 0;
                return false;
            }

            pixels = this.front;
            width = this.frontWidth;
            height = this.frontHeight;
            return true;
        }
    }

    public void Dispose() => this.Stop();

    private void Run(nint window)
    {
        nint device = 0, context = 0, dxgiDevice = 0, winrtDevice = 0, interop = 0, item = 0, statics = 0, pool = 0, session = 0, staging = 0;
        var stagingWidth = 0u;
        var stagingHeight = 0u;

        try
        {
            RoInitialize(1);

            var hr = D3D11CreateDevice(0, 1, 0, 0x20, 0, 0, 7, out device, out _, out context);
            Check(hr, "creating a Direct3D device");
            Check(QueryInterface(device, IidDxgiDevice, out dxgiDevice), "DXGI device");
            Check(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out winrtDevice), "wrapping the device for capture");

            Check(Factory("Windows.Graphics.Capture.GraphicsCaptureItem", IidGraphicsCaptureItemInterop, out interop), "the capture item factory");
            var iid = IidGraphicsCaptureItem;
            var createForWindow = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, nint*, int>)Vtbl(interop)[SlotInteropCreateForWindow];
            nint created;
            Check(createForWindow(interop, window, &iid, &created), "capturing that window");
            item = created;

            SizeInt32 size;
            var getSize = (delegate* unmanaged[Stdcall]<nint, SizeInt32*, int>)Vtbl(item)[SlotItemGetSize];
            Check(getSize(item, &size), "the window's size");
            if (size.Width <= 0 || size.Height <= 0)
                throw new InvalidOperationException("That window has no size; is it minimised?");

            Check(Factory("Windows.Graphics.Capture.Direct3D11CaptureFramePool", IidFramePoolStatics2, out statics), "the frame pool factory");
            var createPool = (delegate* unmanaged[Stdcall]<nint, nint, int, int, SizeInt32, nint*, int>)Vtbl(statics)[SlotStaticsCreateFreeThreaded];
            nint createdPool;
            Check(createPool(statics, winrtDevice, PixelFormatB8G8R8A8, 2, size, &createdPool), "the frame pool");
            pool = createdPool;

            var createSession = (delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)Vtbl(pool)[SlotPoolCreateCaptureSession];
            nint createdSession;
            Check(createSession(pool, item, &createdSession), "the capture session");
            session = createdSession;

            // The cursor is left out and the yellow border asked off; neither is essential, and
            // older builds refuse one or the other, so failures here are ignored.
            if (QueryInterface(session, IidGraphicsCaptureSession2, out var session2) >= 0)
            {
                ((delegate* unmanaged[Stdcall]<nint, byte, int>)Vtbl(session2)[SlotSession2PutCursor])(session2, 0);
                Release(session2);
            }

            if (QueryInterface(session, IidGraphicsCaptureSession3, out var session3) >= 0)
            {
                ((delegate* unmanaged[Stdcall]<nint, byte, int>)Vtbl(session3)[SlotSession3PutBorder])(session3, 0);
                Release(session3);
            }

            Check(((delegate* unmanaged[Stdcall]<nint, int>)Vtbl(session)[SlotSessionStartCapture])(session), "starting the capture");

            var tryGetNextFrame = (delegate* unmanaged[Stdcall]<nint, nint*, int>)Vtbl(pool)[SlotPoolTryGetNextFrame];
            var recreate = (delegate* unmanaged[Stdcall]<nint, nint, int, int, SizeInt32, int>)Vtbl(pool)[SlotPoolRecreate];
            var poolSize = size;

            while (this.running)
            {
                nint frame;
                if (tryGetNextFrame(pool, &frame) < 0 || frame == 0)
                {
                    Thread.Sleep(4);
                    continue;
                }

                try
                {
                    SizeInt32 content;
                    ((delegate* unmanaged[Stdcall]<nint, SizeInt32*, int>)Vtbl(frame)[SlotFrameGetContentSize])(frame, &content);

                    nint surface;
                    Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Vtbl(frame)[SlotFrameGetSurface])(frame, &surface), "the frame's surface");
                    try
                    {
                        Check(QueryInterface(surface, IidDxgiInterfaceAccess, out var access), "the surface's texture");
                        nint texture;
                        try
                        {
                            var texIid = IidTexture2D;
                            Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Vtbl(access)[SlotAccessGetInterface])(access, &texIid, &texture), "the frame's texture");
                        }
                        finally
                        {
                            Release(access);
                        }

                        try
                        {
                            Texture2DDesc desc;
                            ((delegate* unmanaged[Stdcall]<nint, Texture2DDesc*, void>)Vtbl(texture)[SlotTextureGetDesc])(texture, &desc);

                            if (staging == 0 || desc.Width != stagingWidth || desc.Height != stagingHeight)
                            {
                                if (staging != 0)
                                    Release(staging);
                                var stagingDesc = desc;
                                stagingDesc.Usage = D3D11UsageStaging;
                                stagingDesc.BindFlags = 0;
                                stagingDesc.CpuAccessFlags = D3D11CpuAccessRead;
                                stagingDesc.MiscFlags = 0;
                                nint made;
                                Check(((delegate* unmanaged[Stdcall]<nint, Texture2DDesc*, nint, nint*, int>)Vtbl(device)[SlotDeviceCreateTexture2D])(device, &stagingDesc, 0, &made), "a staging texture");
                                staging = made;
                                stagingWidth = desc.Width;
                                stagingHeight = desc.Height;
                            }

                            ((delegate* unmanaged[Stdcall]<nint, nint, nint, void>)Vtbl(context)[SlotContextCopyResource])(context, staging, texture);

                            MappedSubresource mapped;
                            Check(((delegate* unmanaged[Stdcall]<nint, nint, uint, uint, uint, MappedSubresource*, int>)Vtbl(context)[SlotContextMap])(context, staging, 0, D3D11MapRead, 0, &mapped), "reading the frame");
                            try
                            {
                                var width = Math.Min(content.Width > 0 ? content.Width : (int)desc.Width, (int)desc.Width);
                                var height = Math.Min(content.Height > 0 ? content.Height : (int)desc.Height, (int)desc.Height);
                                this.Publish(mapped, width, height);
                            }
                            finally
                            {
                                ((delegate* unmanaged[Stdcall]<nint, nint, uint, void>)Vtbl(context)[SlotContextUnmap])(context, staging, 0);
                            }
                        }
                        finally
                        {
                            Release(texture);
                        }
                    }
                    finally
                    {
                        Release(surface);
                    }

                    // A window that changed size gets a pool of the new size, from the next frame.
                    if (content.Width > 0 && content.Height > 0 && (content.Width != poolSize.Width || content.Height != poolSize.Height))
                    {
                        poolSize = content;
                        recreate(pool, winrtDevice, PixelFormatB8G8R8A8, 2, poolSize);
                    }
                }
                finally
                {
                    Close(frame);
                    Release(frame);
                }
            }
        }
        catch (Exception ex)
        {
            this.Error = ex.Message;
        }
        finally
        {
            this.running = false;
            if (session != 0)
            {
                Close(session);
                Release(session);
            }

            if (pool != 0)
            {
                Close(pool);
                Release(pool);
            }

            foreach (var handle in new[] { item, interop, statics, staging, winrtDevice, dxgiDevice, context, device })
            {
                if (handle != 0)
                    Release(handle);
            }
        }
    }

    /// <summary>BGRA rows from the mapped texture into the back buffer as RGBA, then swapped to the front.</summary>
    private void Publish(MappedSubresource mapped, int width, int height)
    {
        var needed = width * height;
        if (this.back is null || this.back.Length != needed)
            this.back = new uint[needed];

        var target = this.back;
        fixed (uint* dst = target)
        {
            for (var y = 0; y < height; y++)
            {
                var row = (uint*)((byte*)mapped.Data + (y * mapped.RowPitch));
                var outRow = dst + (y * width);
                for (var x = 0; x < width; x++)
                {
                    var bgra = row[x];
                    outRow[x] = 0xFF000000u | ((bgra & 0x00FF0000u) >> 16) | (bgra & 0x0000FF00u) | ((bgra & 0x000000FFu) << 16);
                }
            }
        }

        lock (this.gate)
        {
            (this.front, this.back) = (target, this.front);
            this.frontWidth = width;
            this.frontHeight = height;
        }

        Interlocked.Increment(ref this.frames);
    }

    // -- COM plumbing ----------------------------------------------------------------------------

    private static void** Vtbl(nint obj) => *(void***)obj;

    private static int QueryInterface(nint obj, Guid iid, out nint result)
    {
        nint found;
        var hr = ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Vtbl(obj)[SlotQueryInterface])(obj, &iid, &found);
        result = hr >= 0 ? found : 0;
        return hr;
    }

    private static void Release(nint obj) => ((delegate* unmanaged[Stdcall]<nint, uint>)Vtbl(obj)[SlotRelease])(obj);

    private static void Close(nint winrtObject)
    {
        if (QueryInterface(winrtObject, IidClosable, out var closable) >= 0)
        {
            ((delegate* unmanaged[Stdcall]<nint, int>)Vtbl(closable)[SlotClosableClose])(closable);
            Release(closable);
        }
    }

    private static int Factory(string runtimeClass, Guid iid, out nint factory)
    {
        var hr = WindowsCreateString(runtimeClass, runtimeClass.Length, out var name);
        if (hr < 0)
        {
            factory = 0;
            return hr;
        }

        try
        {
            return RoGetActivationFactory(name, ref iid, out factory);
        }
        finally
        {
            WindowsDeleteString(name);
        }
    }

    private static void Check(int hr, string what)
    {
        if (hr < 0)
            throw new InvalidOperationException($"Windows refused {what} (0x{hr:X8}).");
    }

    private delegate bool EnumWindowsProc(nint hwnd, nint lparam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lparam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(nint hwnd, StringBuilder text, int max);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, uint* pid);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, void* value, int size);

    [DllImport("combase.dll")]
    private static extern int RoInitialize(int type);

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string source, int length, out nint hstring);

    [DllImport("combase.dll")]
    private static extern int WindowsDeleteString(nint hstring);

    [DllImport("combase.dll")]
    private static extern int RoGetActivationFactory(nint classId, ref Guid iid, out nint factory);

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(nint adapter, int driverType, nint software, uint flags, nint featureLevels, uint featureLevelCount, uint sdkVersion, out nint device, out int featureLevel, out nint context);

    [DllImport("d3d11.dll")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);
}
