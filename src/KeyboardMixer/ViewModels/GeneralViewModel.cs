using KeyboardMixer.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyboardMixer.Core.Audio;
using KeyboardMixer.Core.Settings;
using KeyboardMixer.Services;

namespace KeyboardMixer.ViewModels;

public sealed partial class GeneralViewModel : ObservableObject
{
    private readonly SettingsService _settings = App.Current.Settings;

    public GeneralViewModel()
    {
        _startWithWindows = StartupService.IsEnabled;
    }

    public IReadOnlyList<string> MonitorOptions { get; } =
    [
        "Primary monitor",
        "Monitor with the mouse cursor",
        "Monitor with the focused window",
    ];

    // Same order as the FlyoutPosition enum.
    public IReadOnlyList<string> PositionOptions { get; } =
    [
        "Bottom center",
        "Top center",
        "Bottom right",
        "Bottom left",
        "Top right",
        "Top left",
    ];

    public double MinVolumeStep => VolumeMath.MinStep;
    public double MaxVolumeStep => VolumeMath.MaxStep;

    private bool _startWithWindows;

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (_startWithWindows == value) return;
            try
            {
                StartupService.SetEnabled(value);
                _startWithWindows = value;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to change startup setting", ex);
                _startWithWindows = StartupService.IsEnabled;
            }
            OnPropertyChanged();
        }
    }

    public bool LaunchMinimized
    {
        get => _settings.Current.LaunchMinimized;
        set => Update(_settings.Current.LaunchMinimized != value, () => _settings.Current.LaunchMinimized = value);
    }

    public double VolumeStep
    {
        get => _settings.Current.VolumeStep;
        set
        {
            // NumberBox reports NaN when cleared; ignore that and keep the current value.
            if (double.IsNaN(value)) { OnPropertyChanged(); return; }
            int step = VolumeMath.ClampStep((int)Math.Round(value));
            Update(_settings.Current.VolumeStep != step, () => _settings.Current.VolumeStep = step);
        }
    }

    public int MonitorIndex
    {
        get => (int)_settings.Current.Flyout.Monitor;
        set => Update(value >= 0 && value != MonitorIndex, () => _settings.Current.Flyout.Monitor = (FlyoutMonitor)value);
    }

    public int PositionIndex
    {
        get => (int)_settings.Current.Flyout.Position;
        set => Update(value >= 0 && value != PositionIndex, () => _settings.Current.Flyout.Position = (FlyoutPosition)value);
    }

    [RelayCommand]
    private void PreviewFlyout() => App.Current.Flyout.ShowPreview();

    private void Update(bool changed, Action apply, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        if (!changed) return;
        apply();
        _settings.Save();
        OnPropertyChanged(property);
    }
}
