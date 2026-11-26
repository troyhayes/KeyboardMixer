using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace KeyboardMixer.Core.Processes;

public sealed record ProcessIdentity(int Pid, string ExeName, string? ExePath);

/// <summary>Finds the process that owns the foreground window.</summary>
public static partial class ForegroundApp
{
    public static ProcessIdentity? Get()
    {
        nint hwnd = GetForegroundWindow();
        if (hwnd == 0) return null;

        GetWindowThreadProcessId(hwnd, out uint pid);
        var path = ProcessInfo.GetExePath((int)pid);
        if (path is null) return null;

        // UWP / packaged apps are hosted inside ApplicationFrameHost.exe; the real app is the
        // process that owns one of the frame's child windows.
        if (string.Equals(Path.GetFileName(path), "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
        {
            uint childPid = FindChildWindowProcess(hwnd, pid);
            if (childPid != 0)
            {
                var childPath = ProcessInfo.GetExePath((int)childPid);
                if (childPath is not null)
                {
                    pid = childPid;
                    path = childPath;
                }
            }
        }

        return new ProcessIdentity((int)pid, Path.GetFileName(path), path);
    }

    private sealed class ChildSearch(uint hostPid)
    {
        public uint HostPid { get; } = hostPid;
        public uint FoundPid { get; set; }
    }

    private static unsafe uint FindChildWindowProcess(nint parent, uint hostPid)
    {
        var search = new ChildSearch(hostPid);
        var handle = GCHandle.Alloc(search);
        try
        {
            EnumChildWindows(parent, &EnumChildProc, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
        return search.FoundPid;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int EnumChildProc(nint hwnd, nint lParam)
    {
        var search = (ChildSearch)GCHandle.FromIntPtr(lParam).Target!;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid != 0 && pid != search.HostPid)
        {
            search.FoundPid = pid;
            return 0; // stop
        }
        return 1; // continue
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool EnumChildWindows(nint parent, delegate* unmanaged[Stdcall]<nint, nint, int> callback, nint lParam);
}
