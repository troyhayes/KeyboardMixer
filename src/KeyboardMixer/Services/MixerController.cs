using KeyboardMixer.Core;
using System.Diagnostics;
using KeyboardMixer.Core.Hotkeys;
using KeyboardMixer.Core.Processes;
using KeyboardMixer.Views;

namespace KeyboardMixer.Services;

/// <summary>Turns hotkey presses into volume and mute changes and shows the result in the flyout.</summary>
internal sealed class MixerController
{
    private readonly SettingsService _settings;
    private readonly AudioWorker _audio;
    private readonly FlyoutWindow _flyout;

    public MixerController(SettingsService settings, HotkeyService hotkeys, AudioWorker audio, FlyoutWindow flyout)
    {
        _settings = settings;
        _audio = audio;
        _flyout = flyout;
        hotkeys.Pressed += OnPressed;
    }

    private async void OnPressed(Guid bindingId, BindingAction action)
    {
        try
        {
            var binding = _settings.Current.Bindings.FirstOrDefault(b => b.Id == bindingId);
            if (binding is null) return;

            string exeName;
            string? exePath;
            if (binding.Target == BindingTarget.FocusedApp)
            {
                var foreground = ForegroundApp.Get();
                if (foreground is null) return;
                exeName = foreground.ExeName;
                exePath = foreground.ExePath;
            }
            else
            {
                exeName = binding.ExeName!;
                exePath = binding.ExePath;
            }

            int step = _settings.Current.VolumeStep;
            var change = await _audio.RunAsync(audio => action == BindingAction.ToggleMute
                ? audio.ToggleMute(exeName, exePath)
                : audio.StepVolume(exeName, exePath, step, up: action == BindingAction.VolumeUp));

            // Remember where a bound app lives so its icon shows even when it isn't running.
            if (binding.Target == BindingTarget.Application && change.ExePath is not null &&
                !string.Equals(binding.ExePath, change.ExePath, StringComparison.OrdinalIgnoreCase))
            {
                binding.ExePath = change.ExePath;
                _settings.Save();
            }

            _flyout.ShowChange(change);
        }
        catch (Exception ex)
        {
            Log.Error("Volume change failed", ex);
        }
    }
}
