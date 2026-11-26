using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyboardMixer.Core.Hotkeys;
using KeyboardMixer.Services;

namespace KeyboardMixer.ViewModels;

public sealed partial class ShortcutsViewModel : ObservableObject
{
    private readonly SettingsService _settings = App.Current.Settings;
    private readonly HotkeyService _hotkeys = App.Current.Hotkeys;

    public ShortcutsViewModel()
    {
        Focused = CreateItem(_settings.Current.FocusedAppBinding);
        Reload();
    }

    [ObservableProperty]
    private BindingItemViewModel _focused;

    public ObservableCollection<BindingItemViewModel> Apps { get; } = [];

    public bool HasNoApps => Apps.Count == 0;

    public bool IsPaused => _hotkeys.IsPaused;

    internal IReadOnlyList<Binding> AllBindings => _settings.Current.Bindings;

    /// <summary>Subscribe while the page is visible.</summary>
    public void Attach() => _hotkeys.StateChanged += OnHotkeyStateChanged;

    public void Detach() => _hotkeys.StateChanged -= OnHotkeyStateChanged;

    [RelayCommand]
    private void Resume() => _hotkeys.SetPaused(false);

    internal void Add(Binding binding)
    {
        _settings.Current.Bindings.Add(binding);
        Save();
        Reload();
    }

    /// <summary>Replaces the binding with the same Id.</summary>
    internal void Replace(Binding binding)
    {
        var list = _settings.Current.Bindings;
        int index = list.FindIndex(b => b.Id == binding.Id);
        if (index < 0) return;
        list[index] = binding;
        Save();
        Reload();
    }

    internal void Delete(BindingItemViewModel item)
    {
        _settings.Current.Bindings.RemoveAll(b => b.Id == item.Model.Id);
        Save();
        Reload();
    }

    private void Save() => _settings.Save();

    private void Reload()
    {
        Focused = CreateItem(_settings.Current.FocusedAppBinding);
        Apps.Clear();
        foreach (var binding in _settings.Current.AppBindings) Apps.Add(CreateItem(binding));
        OnPropertyChanged(nameof(HasNoApps));
        ApplyErrors();
    }

    private BindingItemViewModel CreateItem(Binding binding) => new(binding, Save);

    private void OnHotkeyStateChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsPaused));
        ApplyErrors();
    }

    private void ApplyErrors()
    {
        foreach (var item in Apps.Prepend(Focused))
            item.Error = _hotkeys.Errors.TryGetValue(item.Model.Id, out var error) ? error : null;
    }
}
