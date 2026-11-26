using System.Text.Json.Serialization;

namespace KeyboardMixer.Core.Hotkeys;

public enum BindingTarget
{
    /// <summary>Whatever application owns the foreground window when the shortcut is pressed.</summary>
    FocusedApp,

    /// <summary>A specific application, identified by its executable name.</summary>
    Application,
}

/// <summary>What a shortcut key does to the target app's audio.</summary>
public enum BindingAction
{
    VolumeUp,
    VolumeDown,
    ToggleMute,
}

/// <summary>
/// A shortcut: hold <see cref="Modifiers"/> and press <see cref="UpKey"/>, <see cref="DownKey"/>
/// or <see cref="MuteKey"/> to change the volume of <see cref="Target"/>. By default the keys are
/// the keyboard's Volume Up, Volume Down and Mute keys.
/// </summary>
public sealed class Binding
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public HotkeyModifiers Modifiers { get; set; }

    /// <summary>Virtual-key code that raises the volume.</summary>
    public int UpKey { get; set; } = VirtualKeys.VolumeUp;

    /// <summary>Virtual-key code that lowers the volume.</summary>
    public int DownKey { get; set; } = VirtualKeys.VolumeDown;

    /// <summary>Virtual-key code that toggles mute, or 0 for no mute key.</summary>
    public int MuteKey { get; set; } = VirtualKeys.VolumeMute;

    public BindingTarget Target { get; set; }

    /// <summary>Executable file name, e.g. "spotify.exe". Only used when <see cref="Target"/> is Application.</summary>
    public string? ExeName { get; set; }

    /// <summary>Friendly name shown in the UI, e.g. "Spotify".</summary>
    public string? DisplayName { get; set; }

    /// <summary>Last known full path of the executable, used to show its icon when it isn't running.</summary>
    public string? ExePath { get; set; }

    public bool Enabled { get; set; } = true;

    public Binding Clone() => (Binding)MemberwiseClone();

    public string Describe() => Target == BindingTarget.FocusedApp
        ? "Focused app"
        : DisplayName ?? ExeName ?? "Unknown app";

    /// <summary>True when the keys are the keyboard's own volume keys.</summary>
    [JsonIgnore]
    public bool UsesMediaKeys =>
        UpKey == VirtualKeys.VolumeUp && DownKey == VirtualKeys.VolumeDown && MuteKey is VirtualKeys.VolumeMute or 0;

    public void ResetKeys()
    {
        UpKey = VirtualKeys.VolumeUp;
        DownKey = VirtualKeys.VolumeDown;
        MuteKey = VirtualKeys.VolumeMute;
    }

    public int GetKey(BindingAction action) => action switch
    {
        BindingAction.VolumeUp => UpKey,
        BindingAction.VolumeDown => DownKey,
        _ => MuteKey,
    };

    public void SetKey(BindingAction action, int vk)
    {
        switch (action)
        {
            case BindingAction.VolumeUp: UpKey = vk; break;
            case BindingAction.VolumeDown: DownKey = vk; break;
            default: MuteKey = vk; break;
        }
    }

    /// <summary>The keys this binding registers, skipping an unset mute key.</summary>
    public IEnumerable<(BindingAction Action, int Key)> GetKeys()
    {
        yield return (BindingAction.VolumeUp, UpKey);
        yield return (BindingAction.VolumeDown, DownKey);
        if (MuteKey != 0) yield return (BindingAction.ToggleMute, MuteKey);
    }

    /// <summary>Full description of the keys, e.g. "Ctrl + Alt + F10 / F9, F11 to mute".</summary>
    public string DescribeKeys()
    {
        string prefix = Modifiers == HotkeyModifiers.None ? "" : Modifiers.ToDisplayString() + " + ";
        if (UsesMediaKeys)
            return prefix + (MuteKey == 0 ? "Volume Up or Down" : "Volume Up, Volume Down or Mute");

        string keys = $"{VirtualKeys.GetName(UpKey)} (up), {VirtualKeys.GetName(DownKey)} (down)";
        if (MuteKey != 0) keys += $", {VirtualKeys.GetName(MuteKey)} (mute)";
        return prefix + keys;
    }
}
