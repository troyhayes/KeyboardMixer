using KeyboardMixer.Core.Hotkeys;

namespace KeyboardMixer.Core.Tests;

public class BindingValidatorTests
{
    private static readonly Binding Focused = new() { Target = BindingTarget.FocusedApp, Modifiers = HotkeyModifiers.Ctrl };

    private static readonly Binding Spotify = new()
    {
        Target = BindingTarget.Application,
        Modifiers = HotkeyModifiers.Ctrl | HotkeyModifiers.Shift,
        ExeName = "spotify.exe",
        DisplayName = "Spotify",
    };

    [Fact]
    public void RequiresModifier()
    {
        var b = new Binding { Target = BindingTarget.Application, ExeName = "a.exe" };
        Assert.False(BindingValidator.Validate(b, []).IsValid);
    }

    [Fact]
    public void RequiresApp()
    {
        var b = new Binding { Target = BindingTarget.Application, Modifiers = HotkeyModifiers.Alt };
        Assert.Equal("Choose an app.", BindingValidator.Validate(b, []).Error);
    }

    [Fact]
    public void RejectsDuplicateModifiers_NamingTheConflict()
    {
        var b = new Binding { Target = BindingTarget.Application, Modifiers = HotkeyModifiers.Ctrl, ExeName = "discord.exe" };
        var result = BindingValidator.Validate(b, [Focused, Spotify]);
        Assert.False(result.IsValid);
        Assert.Contains("Focused app", result.Error);
    }

    [Fact]
    public void EditingItself_IsNotAConflict()
    {
        var edited = Spotify.Clone();
        edited.ExeName = "spotify2.exe";
        Assert.True(BindingValidator.Validate(edited, [Focused, Spotify]).IsValid);
    }

    [Fact]
    public void DistinctModifierSets_AreValid()
    {
        var b = new Binding { Target = BindingTarget.Application, Modifiers = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, ExeName = "firefox" };
        Assert.True(BindingValidator.Validate(b, [Focused, Spotify]).IsValid);
    }

    [Fact]
    public void SameModifiers_WithDifferentKeys_AreValid()
    {
        var b = new Binding { Target = BindingTarget.Application, Modifiers = HotkeyModifiers.Ctrl, ExeName = "discord.exe", UpKey = 0x79, DownKey = 0x78, MuteKey = 0x7A };
        Assert.True(BindingValidator.Validate(b, [Focused, Spotify]).IsValid);
    }

    [Fact]
    public void SharedKey_WithSameModifiers_NamesTheCombo()
    {
        // Volume Up / Down are custom, but the mute key is still the keyboard's Mute key.
        var b = new Binding { Target = BindingTarget.Application, Modifiers = HotkeyModifiers.Ctrl, ExeName = "discord.exe", UpKey = 0x79, DownKey = 0x78 };
        var result = BindingValidator.Validate(b, [Focused]);
        Assert.False(result.IsValid);
        Assert.Equal("Ctrl + Mute is already used by \"Focused app\".", result.Error);
    }

    [Fact]
    public void NoMuteKey_IsAllowed_AndAvoidsConflict()
    {
        var b = new Binding { Target = BindingTarget.Application, Modifiers = HotkeyModifiers.Ctrl, ExeName = "discord.exe", UpKey = 0x79, DownKey = 0x78, MuteKey = 0 };
        Assert.True(BindingValidator.Validate(b, [Focused]).IsValid);
    }

    [Fact]
    public void RejectsSameKeyForTwoActions()
    {
        var b = new Binding { Target = BindingTarget.Application, Modifiers = HotkeyModifiers.Alt, ExeName = "a.exe", UpKey = 0x79, DownKey = 0x79 };
        Assert.Equal("Use a different key for each action.", BindingValidator.Validate(b, []).Error);
    }

    [Theory]
    [InlineData(0x10)] // Shift
    [InlineData(0xA2)] // Left Ctrl
    [InlineData(0x5B)] // Win
    [InlineData(0x7B)] // F12, reserved
    [InlineData(0)]
    public void RejectsUnusableKeys(int vk)
    {
        var b = new Binding { Target = BindingTarget.Application, Modifiers = HotkeyModifiers.Alt, ExeName = "a.exe", UpKey = vk };
        Assert.False(BindingValidator.Validate(b, []).IsValid);
    }

    [Fact]
    public void F13ToF24_WorkWithoutModifiers()
    {
        var b = new Binding { Target = BindingTarget.Application, ExeName = "a.exe", UpKey = VirtualKeys.F13, DownKey = VirtualKeys.F13 + 1, MuteKey = VirtualKeys.F24 };
        Assert.True(BindingValidator.Validate(b, []).IsValid);
    }

    [Fact]
    public void TypingKeys_NeedModifiers()
    {
        var b = new Binding { Target = BindingTarget.Application, ExeName = "a.exe", UpKey = 0x79, DownKey = 0x78, MuteKey = 0 };
        Assert.Contains("modifier", BindingValidator.Validate(b, []).Error);
    }

    [Fact]
    public void DescribeKeys()
    {
        Assert.Equal("Ctrl + Volume Up, Volume Down or Mute", Focused.DescribeKeys());
        var custom = new Binding { Modifiers = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, UpKey = 0x79, DownKey = 0x78, MuteKey = 0 };
        Assert.Equal("Ctrl + Alt + F10 (up), F9 (down)", custom.DescribeKeys());
    }

    [Theory]
    [InlineData(0x41, "A")]
    [InlineData(0x35, "5")]
    [InlineData(0x87, "F24")]
    [InlineData(0x67, "Num 7")]
    [InlineData(0xB3, "Play/Pause")]
    [InlineData(0xFE, "Key FE")]
    public void KeyNames(int vk, string name) => Assert.Equal(name, VirtualKeys.GetName(vk));

    [Fact]
    public void ModifierDisplayOrder() =>
        Assert.Equal(
            "Win + Ctrl + Alt + Shift",
            (HotkeyModifiers.Shift | HotkeyModifiers.Alt | HotkeyModifiers.Ctrl | HotkeyModifiers.Win).ToDisplayString());
}
