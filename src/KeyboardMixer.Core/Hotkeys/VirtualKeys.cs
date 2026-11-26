namespace KeyboardMixer.Core.Hotkeys;

/// <summary>Win32 virtual-key codes used for shortcut keys, plus display names for them.</summary>
public static class VirtualKeys
{
    public const int VolumeMute = 0xAD;
    public const int VolumeDown = 0xAE;
    public const int VolumeUp = 0xAF;

    public const int F1 = 0x70;
    public const int F13 = 0x7C;
    public const int F24 = 0x87;

    /// <summary>
    /// True for keys that can be the main key of a shortcut: anything except modifiers, mouse
    /// buttons and keys Windows reserves (F12 is reserved for debuggers by RegisterHotKey).
    /// </summary>
    public static bool IsAllowed(int vk) => vk switch
    {
        <= 0x07 => false,                     // none, mouse buttons, Cancel
        0x10 or 0x11 or 0x12 => false,        // Shift, Ctrl, Alt
        >= 0xA0 and <= 0xA5 => false,         // left/right Shift, Ctrl, Alt
        0x5B or 0x5C => false,                // Win keys
        0x14 or 0x90 or 0x91 => false,        // Caps, Num and Scroll Lock (toggle state would change)
        0x7B => false,                        // F12
        > 0xFE => false,
        _ => true,
    };

    /// <summary>Keys that can be used without any modifier because they aren't used for typing or system volume.</summary>
    public static bool IsSafeWithoutModifiers(int vk) => vk is >= F13 and <= F24;

    public static string GetName(int vk) => vk switch
    {
        VolumeUp => "Volume Up",
        VolumeDown => "Volume Down",
        VolumeMute => "Mute",
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= 0x60 and <= 0x69 => $"Num {vk - 0x60}",
        >= F1 and <= F24 => $"F{vk - F1 + 1}",
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0C => "Clear",
        0x0D => "Enter",
        0x13 => "Pause",
        0x1B => "Esc",
        0x20 => "Space",
        0x21 => "Page Up",
        0x22 => "Page Down",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x2C => "Print Screen",
        0x2D => "Insert",
        0x2E => "Delete",
        0x5D => "Menu",
        0x6A => "Num *",
        0x6B => "Num +",
        0x6D => "Num -",
        0x6E => "Num .",
        0x6F => "Num /",
        0xA6 => "Browser Back",
        0xA7 => "Browser Forward",
        0xA8 => "Browser Refresh",
        0xA9 => "Browser Stop",
        0xAA => "Browser Search",
        0xAB => "Browser Favorites",
        0xAC => "Browser Home",
        0xB0 => "Next Track",
        0xB1 => "Previous Track",
        0xB2 => "Stop",
        0xB3 => "Play/Pause",
        0xB4 => "Mail",
        0xB5 => "Media",
        0xB6 => "App 1",
        0xB7 => "App 2",
        0xBA => ";",
        0xBB => "=",
        0xBC => ",",
        0xBD => "-",
        0xBE => ".",
        0xBF => "/",
        0xC0 => "`",
        0xDB => "[",
        0xDC => "\\",
        0xDD => "]",
        0xDE => "'",
        _ => $"Key {vk:X2}",
    };
}
