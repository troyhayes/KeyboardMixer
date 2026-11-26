namespace KeyboardMixer.Core.Hotkeys;

/// <summary>
/// Modifier keys for a shortcut. Values match the Win32 MOD_* flags used by RegisterHotKey,
/// so they can be passed straight through.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Ctrl = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

public static class HotkeyModifiersExtensions
{
    /// <summary>Key names in the conventional Windows display order (Win, Ctrl, Alt, Shift).</summary>
    public static IReadOnlyList<string> ToKeyNames(this HotkeyModifiers modifiers)
    {
        var keys = new List<string>(4);
        if (modifiers.HasFlag(HotkeyModifiers.Win)) keys.Add("Win");
        if (modifiers.HasFlag(HotkeyModifiers.Ctrl)) keys.Add("Ctrl");
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) keys.Add("Alt");
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) keys.Add("Shift");
        return keys;
    }

    public static string ToDisplayString(this HotkeyModifiers modifiers)
    {
        var keys = modifiers.ToKeyNames();
        return keys.Count == 0 ? "None" : string.Join(" + ", keys);
    }
}
