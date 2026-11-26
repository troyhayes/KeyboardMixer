using KeyboardMixer.Core.Audio;
using KeyboardMixer.Core.Hotkeys;
using KeyboardMixer.Core.Processes;
using System.Text.Json.Serialization;

namespace KeyboardMixer.Core.Settings;

public enum FlyoutMonitor
{
    Primary,
    WithCursor,
    WithFocusedWindow,
}

public enum FlyoutPosition
{
    BottomCenter,
    TopCenter,
    BottomRight,
    BottomLeft,
    TopRight,
    TopLeft,
}

public sealed class FlyoutSettings
{
    public FlyoutMonitor Monitor { get; set; } = FlyoutMonitor.Primary;
    public FlyoutPosition Position { get; set; } = FlyoutPosition.BottomCenter;
}

public sealed class AppSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Percentage points to change per key press.</summary>
    public int VolumeStep { get; set; } = 2;

    /// <summary>Start in the tray instead of showing the settings window when launched manually.</summary>
    public bool LaunchMinimized { get; set; }

    public FlyoutSettings Flyout { get; set; } = new();

    public List<Binding> Bindings { get; set; } = [];

    [JsonIgnore]
    public Binding FocusedAppBinding => Bindings.First(b => b.Target == BindingTarget.FocusedApp);

    [JsonIgnore]
    public IEnumerable<Binding> AppBindings => Bindings.Where(b => b.Target == BindingTarget.Application);

    public static AppSettings CreateDefault()
    {
        var settings = new AppSettings();
        settings.Normalize();
        return settings;
    }

    /// <summary>
    /// Repairs values from an older or hand-edited file: clamps ranges, guarantees exactly one
    /// focused-app binding, and drops broken or duplicate entries.
    /// </summary>
    public void Normalize()
    {
        Version = CurrentVersion;
        VolumeStep = VolumeMath.ClampStep(VolumeStep);
        Flyout ??= new FlyoutSettings();
        if (!Enum.IsDefined(Flyout.Monitor)) Flyout.Monitor = FlyoutMonitor.Primary;
        if (!Enum.IsDefined(Flyout.Position)) Flyout.Position = FlyoutPosition.BottomCenter;

        var seenIds = new HashSet<Guid>();
        var cleaned = new List<Binding>();
        Binding? focused = null;

        foreach (var b in Bindings ?? [])
        {
            if (b is null) continue;
            if (b.Id == Guid.Empty || !seenIds.Add(b.Id)) b.Id = Guid.NewGuid();
            b.Modifiers &= HotkeyModifiers.Alt | HotkeyModifiers.Ctrl | HotkeyModifiers.Shift | HotkeyModifiers.Win;
            if (!VirtualKeys.IsAllowed(b.UpKey) || !VirtualKeys.IsAllowed(b.DownKey) ||
                (b.MuteKey != 0 && !VirtualKeys.IsAllowed(b.MuteKey)))
                b.ResetKeys();

            if (b.Target == BindingTarget.FocusedApp)
            {
                if (focused is not null) continue; // keep only the first
                b.ExeName = null;
                b.DisplayName = null;
                b.ExePath = null;
                focused = b;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(b.ExeName) || !ExeName.IsValid(b.ExeName)) continue;
                b.ExeName = ExeName.Normalize(b.ExeName);
            }
            cleaned.Add(b);
        }

        if (focused is null)
        {
            focused = new Binding { Target = BindingTarget.FocusedApp, Modifiers = HotkeyModifiers.Ctrl };
            cleaned.Insert(0, focused);
        }
        else if (cleaned[0] != focused)
        {
            cleaned.Remove(focused);
            cleaned.Insert(0, focused);
        }

        Bindings = cleaned;
    }
}
