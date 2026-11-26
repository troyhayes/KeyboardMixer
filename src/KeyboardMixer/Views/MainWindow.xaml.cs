using KeyboardMixer.Native;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace KeyboardMixer.Views;

public sealed partial class MainWindow : Window
{
    private const double WidthDip = 820;
    private const double HeightDip = 820;

    private readonly nint _hwnd;
    private bool _positioned;

    public MainWindow()
    {
        InitializeComponent();
        _hwnd = WindowNative.GetWindowHandle(this);

        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon(App.IconPath);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 560;
            presenter.PreferredMinimumHeight = 480;
        }
    }

    /// <summary>Shows, restores and focuses the window, centering it on first show.</summary>
    public void BringToFront()
    {
        if (!_positioned)
        {
            CenterOnCurrentMonitor();
            _positioned = true;
        }

        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            presenter.Restore();

        Activate();
        Win32.SetForegroundWindow(_hwnd);
    }

    private void CenterOnCurrentMonitor()
    {
        Win32.GetCursorPos(out var cursor);
        var area = DisplayArea.GetFromPoint(new PointInt32(cursor.X, cursor.Y), DisplayAreaFallback.Primary);
        double scale = Win32.GetDpiForWindow(_hwnd) / 96.0;
        if (scale <= 0) scale = 1;

        var work = area.WorkArea;
        int width = Math.Min((int)(WidthDip * scale), work.Width);
        int height = Math.Min((int)(HeightDip * scale), work.Height);
        AppWindow.MoveAndResize(new RectInt32(
            work.X + (work.Width - width) / 2,
            work.Y + (work.Height - height) / 2,
            width,
            height));
    }
}
