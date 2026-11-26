using KeyboardMixer.Core;
using System.Collections.Concurrent;
using KeyboardMixer.Core.Audio;

namespace KeyboardMixer.Services;

/// <summary>
/// Runs all Core Audio (COM) work on one dedicated MTA thread so the UI thread never blocks on
/// audio calls, and COM objects are always used from the thread that created them.
/// </summary>
internal sealed class AudioWorker : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private AudioSessionService? _audio;

    public AudioWorker()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "KeyboardMixer audio" };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    public Task<T> RunAsync<T>(Func<AudioSessionService, T> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try
            {
                _audio ??= new AudioSessionService();
                tcs.SetResult(work(_audio));
            }
            catch (Exception ex)
            {
                // Start fresh next time in case the device enumerator itself went bad.
                Log.Error("Audio call failed", ex);
                _audio?.Dispose();
                _audio = null;
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    private void Run()
    {
        foreach (var action in _queue.GetConsumingEnumerable()) action();
        _audio?.Dispose();
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(2));
    }
}
