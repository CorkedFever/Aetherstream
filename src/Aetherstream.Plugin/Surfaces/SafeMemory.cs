using System.Runtime.InteropServices;

namespace Aetherstream.Plugin.Surfaces;

/// <summary>
/// Checks that a pointer into the game's memory is safe to read before it is read.
/// <para>
/// Walking the game's structures means following pointers whose validity we cannot prove from
/// their values alone. A plausibility check — non-null, not absurdly low, counts within range —
/// catches the obvious cases and misses the rest, and the failure mode is not an exception but the
/// whole game disappearing. Asking the kernel whether the page is committed and readable turns
/// that into a boolean, at the cost of one syscall per check.
/// </para>
/// </summary>
internal static unsafe class SafeMemory
{
    private const uint MemCommit = 0x1000;
    private const uint PageNoAccess = 0x01;
    private const uint PageGuard = 0x100;

    /// <summary>Page protections that permit reading.</summary>
    private const uint ReadableProtections =
        0x02 |   // PAGE_READONLY
        0x04 |   // PAGE_READWRITE
        0x08 |   // PAGE_WRITECOPY
        0x20 |   // PAGE_EXECUTE_READ
        0x40 |   // PAGE_EXECUTE_READWRITE
        0x80;    // PAGE_EXECUTE_WRITECOPY

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
    {
        public nint BaseAddress;
        public nint AllocationBase;
        public uint AllocationProtect;
        public nint RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll", SetLastError = false)]
    private static extern nint VirtualQuery(nint address, out MemoryBasicInformation buffer, nint length);

    /// <summary>
    /// True when <paramref name="bytes"/> starting at <paramref name="address"/> are committed and
    /// readable. Spans that cross into a second region are rejected rather than probed further —
    /// the structures being walked are small and never straddle a boundary in practice.
    /// </summary>
    public static bool IsReadable(nint address, int bytes)
    {
        // Below 64 KB is the reserved null region on Windows; nothing valid ever lives there.
        if (address < 0x10000 || bytes <= 0)
            return false;

        var size = Marshal.SizeOf<MemoryBasicInformation>();
        if (VirtualQuery(address, out var info, size) == 0)
            return false;

        if (info.State != MemCommit)
            return false;

        if ((info.Protect & (PageNoAccess | PageGuard)) != 0)
            return false;

        if ((info.Protect & ReadableProtections) == 0)
            return false;

        // The whole span has to sit inside the region we just asked about.
        var end = info.BaseAddress + info.RegionSize;
        return address + bytes <= end;
    }

    public static bool IsReadable(void* address, int bytes) => IsReadable((nint)address, bytes);

    /// <summary>True when the pointer can be read as a structure of the given type.</summary>
    public static bool CanRead<T>(void* address)
        where T : unmanaged => IsReadable((nint)address, sizeof(T));
}
