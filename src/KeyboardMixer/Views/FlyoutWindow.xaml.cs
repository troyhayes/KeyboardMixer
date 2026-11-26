using KeyboardMixer.Core.Audio;
using KeyboardMixer.Core.Flyout;
using KeyboardMixer.Core.Settings;
using KeyboardMixer.Native;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;
using static KeyboardMixer.Native.Win32;

namespace KeyboardMixer.Views;

/// <summary>
/// The on-screen volume indicator. It never takes focus (WS_EX_NOACTIVATE), never shows in
/// Alt+Tab or the taskbar (WS_EX_TOOLWINDOW), and hides itself after a short delay.
/// </summary>
public sealed partial class FlyoutWindow : Window
{
    private const double WidthDip = 320;
    private const double HeightDip = 72;
    private const double MarginDip = 16;
    private static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(1500);

    private readonly nint _hwnd;
    private readonly DispatcherQueueTimer _hideTimer;
    private string? _currentIconPath;
    private bool _isShown;

    public FlyoutWindow()
    {
        InitializeComponent();
        _hwnd = WindowNative.GetWindowHandle(this);

        var presenter = OverlappedPresenter.Create();
        presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;

        long exStyle = GetWindowLongPtr(_hwnd, GWL_EXSTYLE);
        exStyle = (exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW;
        SetWindowLongPtr(_hwnd, GWL_EXSTYLE, (nint)exStyle);

        unsafe
        {
            int corner = DWMWCP_ROUND;
            DwmSetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, &corner, sizeof(int));
        }

        SystemBackdrop = new AlwaysActiveAcrylicBackdrop();

        _hideTimer = DispatcherQueue.CreateTimer();
        _hideTimer.Interval = HideDelay;
        _hideTimer.IsRepeating = false;
        _hideTimer.Tick += (_, _) => HideFlyout();

        // Never actually close: the app reuses this window. App.ExitApp disposes the process.
        AppWindow.Closing += (_, e) =>
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                HideFlyout();
            }
        };
    }

    private bool _allowClose;

    public new void Close()
    {
        _allowClose = true;
        base.Close();
    }

    internal void ShowChange(VolumeChange change)
    {
        AppName.Text = change.DisplayName;

        if (change.VolumePercent is int volume)
        {
            VolumeSlider.Value = volume;
            VolumeSlider.Visibility = Visibility.Visible;
            NoAudioText.Visibility = Visibility.Collapsed;
            VolumeSlider.Opacity = change.IsMuted ? 0.4 : 1.0;
            VolumeText.Text = change.IsMuted ? "Muted" : volume.ToString();
            FallbackIcon.Glyph = change.IsMuted || volume == 0 ? "" : ""; // Mute / Volume
        }
        else
        {
            VolumeSlider.Visibility = Visibility.Collapsed;
            NoAudioText.Visibility = Visibility.Visible;
            VolumeText.Text = "";
            FallbackIcon.Glyph = "";
        }

        _ = UpdateIconAsync(change.ExePath);
        ShowFlyout();
    }

    /// <summary>Shows sample content so the position can be checked from settings.</summary>
    internal void ShowPreview() =>
        ShowChange(new VolumeChange("keyboardmixer.exe", "KeyboardMixer", Environment.ProcessPath, 50));

    private async Task UpdateIconAsync(string? exePath)
    {
        if (string.Equals(exePath, _currentIconPath, StringComparison.OrdinalIgnoreCase)) return;
        _currentIconPath = exePath;
        AppIcon.Source = null;
        FallbackIcon.Visibility = Visibility.Visible;

        var icon = await App.Current.Icons.GetAsync(exePath);
        if (string.Equals(exePath, _currentIconPath, StringComparison.OrdinalIgnoreCase))
        {
            AppIcon.Source = icon;
            // The glyph is only a placeholder; icons with transparency would show it through.
            FallbackIcon.Visibility = icon is null ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ShowFlyout()
    {
        if (!_isShown)
        {
            PlaceOnScreen();
            AppWindow.Show(activateWindow: false);
            _isShown = true;
        }
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void HideFlyout()
    {
        _hideTimer.Stop();
        if (!_isShown) return;
        AppWindow.Hide();
        _isShown = false;
    }

    private void PlaceOnScreen()
    {
        var settings = App.Current.Settings.Current.Flyout;
        var area = GetDisplayArea(settings.Monitor);
        double scale = GetScale(area);

        int width = (int)Math.Round(WidthDip * scale);
        int height = (int)Math.Round(HeightDip * scale);
        int margin = (int)Math.Round(MarginDip * scale);

        var work = area.WorkArea;
        var (x, y) = FlyoutPlacement.Compute(
            new PixelRect(work.X, work.Y, work.Width, work.Height), width, height, settings.Position, margin);

        // Move twice: the first move may cross into a monitor with a different DPI, which makes
        // Windows rescale the window; the second puts it exactly where we want it.
        var rect = new RectInt32(x, y, width, height);
        AppWindow.MoveAndResize(rect);
        AppWindow.MoveAndResize(rect);
    }

    private static DisplayArea GetDisplayArea(FlyoutMonitor monitor)
    {
        switch (monitor)
        {
            case FlyoutMonitor.WithCursor when GetCursorPos(out var point):
                return DisplayArea.GetFromPoint(new PointInt32(point.X, point.Y), DisplayAreaFallback.Primary);

            case FlyoutMonitor.WithFocusedWindow:
                nint foreground = GetForegroundWindow();
                if (foreground != 0)
                    return DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(foreground), DisplayAreaFallback.Primary);
                break;
        }
        return DisplayArea.Primary;
    }

    private static double GetScale(DisplayArea area)
    {
        nint monitor = Win32Interop.GetMonitorFromDisplayId(area.DisplayId);
        return GetDpiForMonitor(monitor, 0 /* MDT_EFFECTIVE_DPI */, out uint dpi, out _) == 0 && dpi > 0
            ? dpi / 96.0
            : 1.0;
    }
}
