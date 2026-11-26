using KeyboardMixer.Core;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using KeyboardMixer.Core.Hotkeys;
using KeyboardMixer.Core.Processes;
using KeyboardMixer.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;

namespace KeyboardMixer.Views;

/// <summary>An entry in the dialog's "apps playing audio" list.</summary>
public sealed partial class AppChoice : ObservableObject
{
    public AppChoice(string exeName, string displayName, string? exePath)
    {
        ExeName = exeName;
        DisplayName = displayName;
        ExePath = exePath;
        Icon = BindingItemViewModel.GenericAppIcon;
    }

    public string ExeName { get; }
    public string DisplayName { get; }
    public string? ExePath { get; }

    [ObservableProperty]
    private ImageSource _icon;
}

/// <summary>Add/edit a shortcut: choose modifier keys, the action keys and (for app shortcuts) the target app.</summary>
public sealed partial class ShortcutDialog : ContentDialog
{
    private readonly IReadOnlyList<Binding> _existing;
    private bool _suppressTextChanged;

    /// <summary>The action whose key is being recorded, or null when not recording.</summary>
    private BindingAction? _recording;

    /// <summary>Swallow the key-up of a captured key so it can't click the focused button.</summary>
    private bool _swallowKeyUp;

    internal ShortcutDialog(Binding working, IReadOnlyList<Binding> existing, bool isNew)
    {
        InitializeComponent();
        Result = working;
        _existing = existing;

        Title = working.Target == BindingTarget.FocusedApp
            ? "Edit focused app shortcut"
            : isNew ? "Add shortcut" : "Edit shortcut";

        WinToggle.IsChecked = working.Modifiers.HasFlag(HotkeyModifiers.Win);
        CtrlToggle.IsChecked = working.Modifiers.HasFlag(HotkeyModifiers.Ctrl);
        AltToggle.IsChecked = working.Modifiers.HasFlag(HotkeyModifiers.Alt);
        ShiftToggle.IsChecked = working.Modifiers.HasFlag(HotkeyModifiers.Shift);
        UpdateKeyButtons();

        // Preview (tunneling) events see keys before the focused button or the dialog's own
        // Esc/Enter handling, so any key can be recorded.
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += OnPreviewKeyUp;

        if (working.Target == BindingTarget.FocusedApp)
        {
            AppSection.Visibility = Visibility.Collapsed;
        }
        else
        {
            SetExeText(working.ExeName ?? "");
            Opened += async (_, _) => await LoadAppsAsync();
        }

        Validate();
    }

    /// <summary>The edited binding. Only meaningful when the dialog returns Primary.</summary>
    internal Binding Result { get; }

    private void OnModifierToggled(object sender, RoutedEventArgs e)
    {
        var modifiers = HotkeyModifiers.None;
        if (WinToggle.IsChecked == true) modifiers |= HotkeyModifiers.Win;
        if (CtrlToggle.IsChecked == true) modifiers |= HotkeyModifiers.Ctrl;
        if (AltToggle.IsChecked == true) modifiers |= HotkeyModifiers.Alt;
        if (ShiftToggle.IsChecked == true) modifiers |= HotkeyModifiers.Shift;
        Result.Modifiers = modifiers;
        Validate();
    }

    private void SetModifiers(HotkeyModifiers modifiers)
    {
        Result.Modifiers = modifiers;
        WinToggle.IsChecked = modifiers.HasFlag(HotkeyModifiers.Win);
        CtrlToggle.IsChecked = modifiers.HasFlag(HotkeyModifiers.Ctrl);
        AltToggle.IsChecked = modifiers.HasFlag(HotkeyModifiers.Alt);
        ShiftToggle.IsChecked = modifiers.HasFlag(HotkeyModifiers.Shift);
    }

    private void OnKeyButtonClick(object sender, RoutedEventArgs e)
    {
        var action = Enum.Parse<BindingAction>((string)((FrameworkElement)sender).Tag);
        _recording = _recording == action ? null : action;
        UpdateKeyButtons();
    }

    private void OnKeyButtonLostFocus(object sender, RoutedEventArgs e)
    {
        if (_recording is null) return;
        _recording = null;
        UpdateKeyButtons();
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_recording is not BindingAction action) return;
        e.Handled = true;

        int vk = (int)e.Key;
        if (e.Key == VirtualKey.Escape)
        {
            _recording = null;
            UpdateKeyButtons();
            return;
        }
        if (!VirtualKeys.IsAllowed(vk)) return; // a modifier on its own: keep waiting for the main key

        // If modifiers are held while pressing the key, take those as the shortcut's modifiers.
        var held = HeldModifiers();
        if (held != HotkeyModifiers.None) SetModifiers(held);

        Result.SetKey(action, vk);
        _recording = null;
        _swallowKeyUp = true;
        UpdateKeyButtons();
        Validate();
    }

    private void OnPreviewKeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (!_swallowKeyUp) return;
        e.Handled = true;
        _swallowKeyUp = false;
    }

    private static HotkeyModifiers HeldModifiers()
    {
        static bool Down(VirtualKey key) =>
            InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

        var modifiers = HotkeyModifiers.None;
        if (Down(VirtualKey.LeftWindows) || Down(VirtualKey.RightWindows)) modifiers |= HotkeyModifiers.Win;
        if (Down(VirtualKey.Control)) modifiers |= HotkeyModifiers.Ctrl;
        if (Down(VirtualKey.Menu)) modifiers |= HotkeyModifiers.Alt;
        if (Down(VirtualKey.Shift)) modifiers |= HotkeyModifiers.Shift;
        return modifiers;
    }

    private void OnClearMuteClick(object sender, RoutedEventArgs e)
    {
        Result.MuteKey = 0;
        _recording = null;
        UpdateKeyButtons();
        Validate();
    }

    private void OnResetKeysClick(object sender, RoutedEventArgs e)
    {
        Result.ResetKeys();
        _recording = null;
        UpdateKeyButtons();
        Validate();
    }

    private void UpdateKeyButtons()
    {
        UpKeyButton.Content = KeyLabel(BindingAction.VolumeUp);
        DownKeyButton.Content = KeyLabel(BindingAction.VolumeDown);
        MuteKeyButton.Content = KeyLabel(BindingAction.ToggleMute);
        ClearMuteButton.Visibility = Result.MuteKey == 0 ? Visibility.Collapsed : Visibility.Visible;
        ResetKeysButton.Visibility = Result.UsesMediaKeys && Result.MuteKey != 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private string KeyLabel(BindingAction action)
    {
        if (_recording == action) return "Press a key…";
        int vk = Result.GetKey(action);
        return vk == 0 ? "None" : VirtualKeys.GetName(vk);
    }

    private void OnExeTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextChanged) return;

        string text = ExeBox.Text.Trim().Trim('"');
        if (AppList.SelectedItem is AppChoice selected && ExeName.Matches(selected.ExeName, text))
            return;

        AppList.SelectedItem = null;
        Result.ExeName = text;
        bool valid = text.Length > 0 && ExeName.IsValid(text);
        Result.ExePath = valid && Path.IsPathRooted(text) && File.Exists(text) ? text : null;
        Result.DisplayName = valid ? ProcessInfo.GetDisplayName(Result.ExePath, ExeName.Normalize(text)) : null;
        Validate();
    }

    private void OnAppSelected(object sender, SelectionChangedEventArgs e)
    {
        if (AppList.SelectedItem is not AppChoice choice) return;
        Result.ExeName = choice.ExeName;
        Result.DisplayName = choice.DisplayName;
        Result.ExePath = choice.ExePath;
        SetExeText(choice.ExeName);
        Validate();
    }

    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker(XamlRoot.ContentIslandEnvironment.AppWindowId)
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                CommitButtonText = "Choose",
            };
            picker.FileTypeFilter.Add(".exe");

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            AppList.SelectedItem = null;
            string exe = Path.GetFileName(file.Path);
            Result.ExeName = exe;
            Result.ExePath = file.Path;
            Result.DisplayName = ProcessInfo.GetDisplayName(file.Path, exe);
            SetExeText(exe);
            Validate();
        }
        catch (Exception ex)
        {
            Log.Error("File picker failed", ex);
        }
    }

    private async Task LoadAppsAsync()
    {
        LoadingRing.IsActive = true;
        try
        {
            var apps = await App.Current.Audio.RunAsync(audio => audio.GetApps());
            var choices = apps.Select(a => new AppChoice(a.ExeName, a.DisplayName, a.ExePath)).ToList();
            AppList.ItemsSource = choices;
            NoAppsText.Visibility = choices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            AppList.Visibility = choices.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            var current = choices.FirstOrDefault(c => ExeName.Matches(c.ExeName, Result.ExeName));
            if (current is not null) AppList.SelectedItem = current;

            foreach (var choice in choices)
            {
                var icon = await App.Current.Icons.GetAsync(choice.ExePath);
                if (icon is not null) choice.Icon = icon;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Couldn't list audio apps", ex);
            NoAppsText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private void SetExeText(string text)
    {
        _suppressTextChanged = true;
        ExeBox.Text = text;
        _suppressTextChanged = false;
    }

    private void Validate()
    {
        var result = BindingValidator.Validate(Result, _existing);
        IsPrimaryButtonEnabled = result.IsValid;

        // Don't nag about the app before the user has had a chance to pick one.
        bool appNotChosenYet = Result.Target == BindingTarget.Application && string.IsNullOrWhiteSpace(Result.ExeName);
        ErrorBar.Message = result.Error ?? "";
        ErrorBar.IsOpen = !result.IsValid && !(appNotChosenYet && BindingValidator.ValidateKeys(Result) is null);
    }
}
