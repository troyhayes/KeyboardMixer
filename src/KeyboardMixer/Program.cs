using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace KeyboardMixer;

public static class Program
{
    private const string MutexName = @"Local\KeyboardMixer.SingleInstance";
    private const string ActivateEventName = @"Local\KeyboardMixer.Activate";

    [STAThread]
    private static int Main(string[] args)
    {
        // Single instance: a second launch just asks the running one to show its window.
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            try
            {
                using var existing = EventWaitHandle.OpenExisting(ActivateEventName);
                existing.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The other instance is still starting up or shutting down.
            }
            return 0;
        }

        using var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        bool startMinimized = args.Contains(App.MinimizedArgument, StringComparer.OrdinalIgnoreCase);

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App(activateEvent, startMinimized);
        });

        GC.KeepAlive(mutex);
        return 0;
    }
}
