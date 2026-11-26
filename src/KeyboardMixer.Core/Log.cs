using System.Diagnostics;
using KeyboardMixer.Core.Settings;

namespace KeyboardMixer.Core;

/// <summary>
/// Minimal diagnostics log at %LocalAppData%\KeyboardMixer\log.txt. The file is reset when it
/// grows past 512 KB, so it never needs cleaning up.
/// </summary>
public static class Log
{
    private const long MaxBytes = 512 * 1024;
    private static readonly object s_lock = new();
    private static readonly string s_path = Path.Combine(SettingsStore.DefaultDirectory, "log.txt");

    public static string FilePath => s_path;

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        Debug.WriteLine(line);
        try
        {
            lock (s_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(s_path)!);
                var info = new FileInfo(s_path);
                if (info.Exists && info.Length > MaxBytes) info.Delete();
                File.AppendAllText(s_path, line + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            // Logging must never break the app.
        }
    }
}
