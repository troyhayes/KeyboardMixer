using System.Runtime.InteropServices;
using KeyboardMixer.Core;
using KeyboardMixer.Core.Hotkeys;
using KeyboardMixer.Native;
using static KeyboardMixer.Native.Win32;

namespace KeyboardMixer.Services;

/// <summary>
/// Registers each enabled binding's keys (by default Modifiers + Volume Up, Volume Down and Mute)
/// as system hotkeys with RegisterHotKey. Windows only fires a hotkey on an exact modifier match and swallows the key,
/// so there is no keyboard hook to time out and no key state to track.
/// </summary>
internal sealed class HotkeyService : IDisposable
{
    private readonly NativeHostWindow _host;
    private readonly Dictionary<int, (Guid BindingId, BindingAction Action)> _registered = [];
    private readonly Dictionary<Guid, string> _errors = [];
    private IReadOnlyList<Binding> _bindings = [];
    private int _nextId = 1;

    public HotkeyService(NativeHostWindow host)
    {
        _host = host;
        _host.HotkeyPressed += OnHotkey;
    }

    /// <summary>A binding's shortcut was pressed.</summary>
    public event Action<Guid, BindingAction>? Pressed;

    /// <summary>Registration results or the paused state changed.</summary>
    public event EventHandler? StateChanged;

    public bool IsPaused { get; private set; }

    /// <summary>Bindings that couldn't be registered, with a reason (usually another app owns the combo).</summary>
    public IReadOnlyDictionary<Guid, string> Errors => _errors;

    public void Apply(IEnumerable<Binding> bindings)
    {
        var next = bindings.Select(b => b.Clone()).ToList();
        bool sameKeys = next.Count == _bindings.Count && next.Zip(_bindings).All(p =>
            p.First.Id == p.Second.Id && p.First.Modifiers == p.Second.Modifiers && p.First.Enabled == p.Second.Enabled &&
            p.First.UpKey == p.Second.UpKey && p.First.DownKey == p.Second.DownKey && p.First.MuteKey == p.Second.MuteKey);
        _bindings = next;

        // Settings are saved for unrelated reasons too (volume step, flyout position...);
        // only touch the system registrations when the keys actually changed.
        if (!sameKeys || _registered.Count == 0) Reregister();
    }

    public void SetPaused(bool paused)
    {
        if (IsPaused == paused) return;
        IsPaused = paused;
        Reregister();
    }

    private void Reregister()
    {
        UnregisterAll();
        _errors.Clear();

        if (!IsPaused)
        {
            foreach (var binding in _bindings.Where(b => b.Enabled && BindingValidator.ValidateKeys(b) is null))
            {
                var keys = binding.GetKeys().ToList();
                var failed = new List<(int Key, int Error)>();
                foreach (var (action, vk) in keys)
                {
                    if (!TryRegister(binding, (uint)vk, action, out int error))
                        failed.Add((vk, error));
                }

                if (failed.Count > 0)
                {
                    int error = failed[0].Error;
                    string names = string.Join(", ", failed.Select(f => BindingValidator.Combo(binding.Modifiers, f.Key)));
                    string combo = failed.Count == keys.Count ? "This shortcut" : names;
                    _errors[binding.Id] = error == ERROR_HOTKEY_ALREADY_REGISTERED
                        ? $"{combo} is already in use by another app."
                        : $"Windows couldn't register {combo} (error {error}).";
                    Log.Info($"Couldn't register {names} for {binding.Describe()} (error {error})");
                }
            }
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool TryRegister(Binding binding, uint vk, BindingAction action, out int error)
    {
        int id = _nextId++;
        if (_nextId > 0xBFFF) _nextId = 1; // application hotkey ids must be in 0x0000-0xBFFF

        if (RegisterHotKey(_host.Handle, id, (uint)binding.Modifiers, vk))
        {
            _registered[id] = (binding.Id, action);
            error = 0;
            return true;
        }
        error = Marshal.GetLastWin32Error();
        return false;
    }

    private void UnregisterAll()
    {
        foreach (var id in _registered.Keys) UnregisterHotKey(_host.Handle, id);
        _registered.Clear();
    }

    private void OnHotkey(int id)
    {
        if (_registered.TryGetValue(id, out var entry))
            Pressed?.Invoke(entry.BindingId, entry.Action);
    }

    public void Dispose()
    {
        _host.HotkeyPressed -= OnHotkey;
        UnregisterAll();
    }
}
