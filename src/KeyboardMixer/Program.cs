using KeyboardMixer.Core;
using KeyboardMixer.Core.Settings;
using KeyboardMixer.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace KeyboardMixer;

public static class Program
{
    private const string MutexName = @"Local\KeyboardMixer.SingleInstance";
    private const string ActivateEventName = @"Local\KeyboardMixer.Activate";
    private const string ExitEventName = @"Local\KeyboardMixer.Exit";

    // Command-line options. --minimized is App.MinimizedArgument (used by the startup entry).
    private const string ShowArgument = "--show";         // always show the settings window
    private const string ExitArgument = "--exit";         // ask a running instance to quit, and wait for it
    private const string SetupArgument = "--setup";       // installer: apply choices, then quit
    private const string StartupArgument = "--startup";   //   with --setup: start with Windows
    private const string UninstallArgument = "--uninstall"; // uninstaller: remove the startup entry, then quit

    [STAThread]
    private static int Main(string[] args)
    {
        bool Has(string option) => args.Contains(option, StringComparer.OrdinalIgnoreCase);

        if (Has(SetupArgument))
            return RunSetup(startWithWindows: Has(StartupArgument), launchMinimized: Has(App.MinimizedArgument));

        if (Has(UninstallArgument))
            return RunUninstall();

        // Single instance: a second launch just asks the running one to show its window (or to exit).
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool isFirstInstance);
        if (Has(ExitArgument))
            return isFirstInstance ? 0 : ExitRunningInstance(mutex);

        if (!isFirstInstance)
        {
            Signal(ActivateEventName);
            return 0;
        }

        using var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        using var exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName);
        bool startMinimized = Has(App.MinimizedArgument) && !Has(ShowArgument);
        bool forceShow = Has(ShowArgument);

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App(activateEvent, exitEvent, startMinimized, forceShow);
        });

        GC.KeepAlive(mutex);
        return 0;
    }

    /// <summary>Applies the installer's choices. Runs without any UI.</summary>
    private static int RunSetup(bool startWithWindows, bool launchMinimized)
    {
        try
        {
            StartupService.SetEnabled(startWithWindows);

            var store = new SettingsStore();
            var settings = store.Load();
            settings.LaunchMinimized = launchMinimized;
            store.Save(settings);

            Log.Info($"Setup: start with Windows {startWithWindows}, start minimized {launchMinimized}");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("Setup failed", ex);
            return 1;
        }
    }

    /// <summary>Removes what the app put outside its own folder. Settings are left to the uninstaller.</summary>
    private static int RunUninstall()
    {
        try
        {
            StartupService.SetEnabled(false);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("Uninstall cleanup failed", ex);
            return 1;
        }
    }

    /// <summary>Tells the running instance to quit and waits (up to 10 s) until it has.</summary>
    private static int ExitRunningInstance(Mutex mutex)
    {
        if (!Signal(ExitEventName)) return 1;
        try
        {
            if (!mutex.WaitOne(TimeSpan.FromSeconds(10))) return 1;
        }
        catch (AbandonedMutexException)
        {
            // The other process ended without releasing the mutex: it's gone either way.
        }
        mutex.ReleaseMutex();
        return 0;
    }

    private static bool Signal(string eventName)
    {
        try
        {
            using var handle = EventWaitHandle.OpenExisting(eventName);
            handle.Set();
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // The other instance is still starting up or shutting down.
            return false;
        }
    }
}
