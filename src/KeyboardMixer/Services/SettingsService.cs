using KeyboardMixer.Core;
using KeyboardMixer.Core.Settings;

namespace KeyboardMixer.Services;

/// <summary>Holds the live settings object and persists it whenever something changes.</summary>
internal sealed class SettingsService
{
    private readonly SettingsStore _store;

    public SettingsService(SettingsStore store)
    {
        _store = store;
        Current = store.Load();
    }

    public AppSettings Current { get; }

    public string FilePath => _store.FilePath;

    /// <summary>Raised after every save. Handlers run on the caller's (UI) thread.</summary>
    public event EventHandler? Changed;

    public void Save()
    {
        Current.Normalize();
        try
        {
            _store.Save(Current);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings stay in memory; the next change retries the write.
            Log.Error("Failed to save settings", ex);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
