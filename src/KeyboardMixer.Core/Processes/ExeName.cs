namespace KeyboardMixer.Core.Processes;

/// <summary>Helpers for comparing and normalizing executable names ("Spotify.exe", "spotify", "C:\...\Spotify.exe").</summary>
public static class ExeName
{
    /// <summary>Reduces user input or a full path to a lower-case file name ending in ".exe".</summary>
    public static string Normalize(string input)
    {
        var name = input.Trim().Trim('"');
        int slash = name.LastIndexOfAny(['\\', '/']);
        if (slash >= 0) name = name[(slash + 1)..];
        if (name.Length > 0 && !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name += ".exe";
        return name.ToLowerInvariant();
    }

    public static bool IsValid(string input)
    {
        var name = Normalize(input);
        return name.Length > ".exe".Length && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    public static bool Matches(string? a, string? b) =>
        a is not null && b is not null &&
        string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);

    /// <summary>"spotify.exe" → "Spotify". Used when an executable has no file description.</summary>
    public static string ToFriendlyName(string exeName)
    {
        var name = Path.GetFileNameWithoutExtension(exeName.Trim());
        return name.Length == 0 ? exeName : char.ToUpperInvariant(name[0]) + name[1..];
    }
}
