using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace KeyboardMixer.Core.Processes;

/// <summary>Looks up executable paths and friendly names for process IDs.</summary>
public static partial class ProcessInfo
{
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    private static readonly ConcurrentDictionary<string, string> s_displayNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Full path of the process's executable, or null if it can't be read (exited, protected, etc.).</summary>
    public static string? GetExePath(int pid)
    {
        if (pid <= 0) return null;

        using SafeProcessHandle handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (handle.IsInvalid) return null;

        Span<char> buffer = stackalloc char[1024];
        uint size = (uint)buffer.Length;
        unsafe
        {
            fixed (char* p = buffer)
            {
                if (!QueryFullProcessImageNameW(handle, 0, p, ref size)) return null;
            }
        }
        return new string(buffer[..(int)size]);
    }

    public static string? GetExeName(int pid)
    {
        var path = GetExePath(pid);
        return path is null ? null : Path.GetFileName(path);
    }

    /// <summary>The executable's file description ("Google Chrome"), falling back to its file name ("Chrome").</summary>
    public static string GetDisplayName(string? exePath, string exeName)
    {
        if (string.IsNullOrEmpty(exePath)) return ExeName.ToFriendlyName(exeName);

        return s_displayNames.GetOrAdd(exePath, static (path, exe) =>
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrWhiteSpace(info.FileDescription)) return info.FileDescription.Trim();
                if (!string.IsNullOrWhiteSpace(info.ProductName)) return info.ProductName.Trim();
            }
            catch (Exception) { /* file unreadable; fall through */ }
            return ExeName.ToFriendlyName(exe);
        }, exeName);
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryFullProcessImageNameW(SafeProcessHandle process, uint flags, char* exeName, ref uint size);
}
