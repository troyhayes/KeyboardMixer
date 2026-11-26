using KeyboardMixer.Core;
using KeyboardMixer.Core.Settings;
using KeyboardMixer.Native;
using KeyboardMixer.Services;
using KeyboardMixer.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace KeyboardMixer;

public partial class App : Application
{
    public const string MinimizedArgument = "--minimized";

    private const int MenuOpenSettings = 1;
    private const int MenuPause = 2;
    private const int MenuExit = 3;

    private readonly EventWaitHandle _activateEvent;
    private readonly EventWaitHandle _exitEvent;
    private readonly bool _startMinimized;
    private readonly bool _forceShow;
    private RegisteredWaitHandle? _activateWait;
    private RegisteredWaitHandle? _exitWait;
    private DispatcherQueue _dispatcher = null!;
    private NativeHostWindow _host = null!;
    private TrayIcon _tray = null!;
    private MainWindow? _mainWindow;
    private bool _exiting;

    public App(EventWaitHandle activateEvent, EventWaitHandle exitEvent, bool startMinimized, bool forceShow)
    {
        _activateEvent = activateEvent;
        _exitEvent = exitEvent;
        _startMinimized = startMinimized;
        _forceShow = forceShow;
        InitializeComponent();

        // We live in the tray: closing the settings window must not end the process.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
        UnhandledException += (_, e) =>
        {
            Log.Error("Unhandled", e.Exception);
            e.Handled = true;
        };
    }

    public static new App Current => (App)Application.Current;

    internal SettingsService Settings { get; private set; } = null!;
    internal HotkeyService Hotkeys { get; private set; } = null!;
    internal AudioWorker Audio { get; private set; } = null!;
    internal IconCache Icons { get; } = new();
    internal FlyoutWindow Flyout { get; private set; } = null!;
    internal MixerController Mixer { get; private set; } = null!;

    public static string IconPath => Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log.Info($"Starting {Environment.ProcessPath} (minimized: {_startMinimized})");
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        Win32.EnableDarkMenus();

        Settings = new SettingsService(new SettingsStore());
        Audio = new AudioWorker();
        Flyout = new FlyoutWindow();

        _host = new NativeHostWindow();
        Hotkeys = new HotkeyService(_host);
        Hotkeys.Apply(Settings.Current.Bindings);
        Settings.Changed += (_, _) => Hotkeys.Apply(Settings.Current.Bindings);

        Mixer = new MixerController(Settings, Hotkeys, Audio, Flyout);

        _tray = new TrayIcon(_host, IconPath, "KeyboardMixer");
        _tray.LeftClick += ShowMainWindow;
        _tray.ContextMenuRequested += ShowTrayMenu;

        StartupService.RefreshPathIfEnabled();

        _activateWait = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent, (_, _) => _dispatcher.TryEnqueue(ShowMainWindow), null, Timeout.Infinite, executeOnlyOnce: false);

        // "KeyboardMixer.exe --exit" (used by the installer before upgrading or uninstalling).
        _exitWait = ThreadPool.RegisterWaitForSingleObject(
            _exitEvent, (_, _) => _dispatcher.TryEnqueue(ExitApp), null, Timeout.Infinite, executeOnlyOnce: true);

        if (_forceShow || (!_startMinimized && !Settings.Current.LaunchMinimized))
            ShowMainWindow();
    }

    public void ShowMainWindow()
    {
        if (_exiting) return;
        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow();
            // Closing releases the window (WinUI windows are heavy); the app keeps running in the tray.
            _mainWindow.Closed += (_, _) => _mainWindow = null;
        }
        _mainWindow.BringToFront();
    }

    private void ShowTrayMenu(int x, int y)
    {
        int command = _tray.ShowMenu(x, y,
        [
            new TrayMenuItem(MenuOpenSettings, "Open settings"),
            new TrayMenuItem(MenuPause, "Pause shortcuts", Hotkeys.IsPaused),
            TrayMenuItem.Separator,
            new TrayMenuItem(MenuExit, "Exit"),
        ]);

        switch (command)
        {
            case MenuOpenSettings: ShowMainWindow(); break;
            case MenuPause: Hotkeys.SetPaused(!Hotkeys.IsPaused); break;
            case MenuExit: ExitApp(); break;
        }
    }

    public void ExitApp()
    {
        if (_exiting) return;
        _exiting = true;

        _activateWait?.Unregister(null);
        _exitWait?.Unregister(null);
        _mainWindow?.Close();
        Flyout.Close();
        Hotkeys.Dispose();
        _tray.Dispose();
        _host.Dispose();
        Audio.Dispose();
        Exit();
    }
}
