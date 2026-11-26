using KeyboardMixer.Core.Hotkeys;
using KeyboardMixer.Core.Settings;

namespace KeyboardMixer.Core.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "KeyboardMixerTests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (DirectoryNotFoundException) { }
    }

    [Fact]
    public void Defaults_HaveCtrlFocusedAppBinding()
    {
        var s = AppSettings.CreateDefault();
        Assert.Equal(2, s.VolumeStep);
        var focused = Assert.Single(s.Bindings);
        Assert.Equal(BindingTarget.FocusedApp, focused.Target);
        Assert.Equal(HotkeyModifiers.Ctrl, focused.Modifiers);
    }

    [Fact]
    public void MissingFile_GivesDefaults() =>
        Assert.Single(new SettingsStore(FilePath).Load().Bindings);

    [Fact]
    public void RoundTrip()
    {
        var store = new SettingsStore(FilePath);
        var s = AppSettings.CreateDefault();
        s.VolumeStep = 5;
        s.Flyout.Position = FlyoutPosition.TopRight;
        s.Bindings.Add(new Binding
        {
            Target = BindingTarget.Application,
            Modifiers = HotkeyModifiers.Ctrl | HotkeyModifiers.Shift,
            ExeName = "Spotify.exe",
            DisplayName = "Spotify",
        });
        store.Save(s);
        store.Save(s); // second save exercises File.Replace

        var loaded = store.Load();
        Assert.Equal(5, loaded.VolumeStep);
        Assert.Equal(FlyoutPosition.TopRight, loaded.Flyout.Position);
        var app = Assert.Single(loaded.AppBindings);
        Assert.Equal("spotify.exe", app.ExeName);
        Assert.Equal(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, app.Modifiers);

        var json = File.ReadAllText(FilePath);
        Assert.Contains("\"Ctrl, Shift\"", json); // enums are stored as readable strings
        Assert.DoesNotContain("appBindings", json); // computed views are not persisted
        Assert.DoesNotContain("focusedAppBinding", json);
    }

    [Fact]
    public void CustomKeys_RoundTrip()
    {
        var store = new SettingsStore(FilePath);
        var s = AppSettings.CreateDefault();
        s.Bindings.Add(new Binding
        {
            Target = BindingTarget.Application,
            Modifiers = HotkeyModifiers.Alt,
            ExeName = "spotify.exe",
            UpKey = 0x79,
            DownKey = 0x78,
            MuteKey = 0,
        });
        store.Save(s);

        var app = Assert.Single(store.Load().AppBindings);
        Assert.Equal((0x79, 0x78, 0), (app.UpKey, app.DownKey, app.MuteKey));
        Assert.DoesNotContain("usesMediaKeys", File.ReadAllText(FilePath));
    }

    [Fact]
    public void FileWithoutKeys_GetsMediaKeys()
    {
        // Settings written before custom keys existed.
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "version": 1, "bindings": [ { "target": "FocusedApp", "modifiers": "Ctrl" } ] }""");
        var focused = new SettingsStore(FilePath).Load().FocusedAppBinding;
        Assert.True(focused.UsesMediaKeys);
        Assert.Equal(VirtualKeys.VolumeMute, focused.MuteKey);
    }

    [Fact]
    public void Normalize_ResetsUnusableKeys()
    {
        var s = new AppSettings { Bindings = [new Binding { Target = BindingTarget.FocusedApp, Modifiers = HotkeyModifiers.Ctrl, UpKey = 0x11 }] };
        s.Normalize();
        Assert.True(s.FocusedAppBinding.UsesMediaKeys);
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaults_AndIsQuarantined()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");
        var loaded = new SettingsStore(FilePath).Load();
        Assert.Single(loaded.Bindings);
        Assert.True(File.Exists(FilePath + ".bad"));
    }

    [Fact]
    public void Normalize_RepairsBadData()
    {
        var id = Guid.NewGuid();
        var s = new AppSettings
        {
            VolumeStep = 500,
            Bindings =
            [
                new Binding { Id = id, Target = BindingTarget.Application, Modifiers = HotkeyModifiers.Alt, ExeName = "ok.exe" },
                new Binding { Id = id, Target = BindingTarget.Application, Modifiers = HotkeyModifiers.Shift, ExeName = "dup-id.exe" },
                new Binding { Target = BindingTarget.Application, Modifiers = HotkeyModifiers.Win, ExeName = "" },
                new Binding { Target = BindingTarget.FocusedApp, Modifiers = HotkeyModifiers.Ctrl },
                new Binding { Target = BindingTarget.FocusedApp, Modifiers = HotkeyModifiers.Alt | HotkeyModifiers.Ctrl },
            ],
        };
        s.Normalize();

        Assert.Equal(25, s.VolumeStep);
        Assert.Equal(3, s.Bindings.Count);
        Assert.Equal(BindingTarget.FocusedApp, s.Bindings[0].Target);
        Assert.Equal(HotkeyModifiers.Ctrl, s.Bindings[0].Modifiers);
        Assert.Equal(s.Bindings.Count, s.Bindings.Select(b => b.Id).Distinct().Count());
    }
}
