using Microsoft.Win32;

namespace KeyboardMixer.Services;

/// <summary>"Start with Windows" via the per-user Run key (no admin rights needed, works unpackaged).</summary>
internal static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KeyboardMixer";

    private static string Command => $"\"{Environment.ProcessPath}\" {App.MinimizedArgument}";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
            key.SetValue(ValueName, Command, RegistryValueKind.String);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>If the app was moved since startup was enabled, point the Run entry at the new location.</summary>
    public static void RefreshPathIfEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(ValueName) is string existing && !string.Equals(existing, Command, StringComparison.OrdinalIgnoreCase))
                key.SetValue(ValueName, Command, RegistryValueKind.String);
        }
        catch (Exception)
        {
            // Not critical.
        }
    }
}
