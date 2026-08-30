// Copied from Memoria (FFXIV-rom-Emulator) src/RomEmulator/Video/D3D11.cs.

using System.Runtime.InteropServices;

using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

namespace Aetherstream.Plugin.Video;

/// <summary>
/// Just enough hand-rolled COM to map a texture. Dalamud exposes no way to write pixels into an
/// <c>IDalamudTextureWrap</c>, so we reach the underlying D3D11 resource ourselves.
/// <para>
/// The vtable slots below are part of the D3D11 ABI, which Microsoft froze in 2009 — they are
/// stable in a way that struct offsets are not. Each one is derived from its inheritance chain in
/// the comments so it can be re-checked without a debugger. The device context comes from
/// FFXIVClientStructs rather than from a vtable walk, because counting forty methods on
/// <c>ID3D11Device</c> is exactly the sort of thing that is wrong once and crashes the game.
/// </para>
/// </summary>
internal static unsafe class D3D11
{
    /// <summary>D3D11_MAP_WRITE_DISCARD.</summary>
    public const uint MapWriteDiscard = 4;

    // IUnknown: 0 QueryInterface, 1 AddRef, 2 Release
    private const int SlotAddRef = 1;
    private const int SlotRelease = 2;

    // ID3D11View : ID3D11DeviceChild : IUnknown
    //   3 GetDevice, 4 GetPrivateData, 5 SetPrivateData, 6 SetPrivateDataInterface
    //   7 GetResource
    private const int SlotGetResource = 7;

    // ID3D11DeviceContext : ID3D11DeviceChild : IUnknown
    //   7 VSSetConstantBuffers, 8 PSSetShaderResources, 9 PSSetShader, 10 PSSetSamplers,
    //   11 VSSetShader, 12 DrawIndexed, 13 Draw, 14 Map, 15 Unmap
    private const int SlotMap = 14;
    private const int SlotUnmap = 15;

    [StructLayout(LayoutKind.Sequential)]
    public struct MappedSubresource
    {
        public void* Data;
        public uint RowPitch;
        public uint DepthPitch;
    }

    /// <summary>
    /// Gets the game's immediate context — the same one Dalamud's ImGui backend draws with, which
    /// is why every call through here has to happen on the render thread.
    /// </summary>
    public static nint ImmediateContext
    {
        get
        {
            var device = Device.Instance();
            return device == null ? 0 : (nint)device->D3D11DeviceContext;
        }
    }

    /// <summary>
    /// Resolves the <c>ID3D11Resource</c> behind a shader resource view. The returned pointer is
    /// AddRef'd and must be passed to <see cref="Release"/>.
    /// </summary>
    public static nint GetResource(nint shaderResourceView)
    {
        var vtbl = *(void***)shaderResourceView;
        var getResource = (delegate* unmanaged[Stdcall]<nint, nint*, void>)vtbl[SlotGetResource];

        nint resource;
        getResource(shaderResourceView, &resource);
        return resource;
    }

    public static int Map(nint context, nint resource, uint mapType, out MappedSubresource mapped)
    {
        var vtbl = *(void***)context;
        var map = (delegate* unmanaged[Stdcall]<nint, nint, uint, uint, uint, MappedSubresource*, int>)vtbl[SlotMap];

        MappedSubresource result;
        var hr = map(context, resource, 0, mapType, 0, &result);
        mapped = result;
        return hr;
    }

    public static void Unmap(nint context, nint resource)
    {
        var vtbl = *(void***)context;
        var unmap = (delegate* unmanaged[Stdcall]<nint, nint, uint, void>)vtbl[SlotUnmap];
        unmap(context, resource, 0);
    }

    /// <summary>
    /// Takes a reference. Needed when handing one of our COM objects to the game: the game may
    /// release what it thinks it owns, and without an extra reference that would free a view we
    /// are still drawing from.
    /// </summary>
    public static void AddRef(nint com)
    {
        if (com == 0)
            return;

        var vtbl = *(void***)com;
        var addRef = (delegate* unmanaged[Stdcall]<nint, uint>)vtbl[SlotAddRef];
        addRef(com);
    }

    public static void Release(nint com)
    {
        if (com == 0)
            return;

        var vtbl = *(void***)com;
        var release = (delegate* unmanaged[Stdcall]<nint, uint>)vtbl[SlotRelease];
        release(com);
    }
}
