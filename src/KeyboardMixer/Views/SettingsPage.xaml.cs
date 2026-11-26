using System.Diagnostics;
using System.Reflection;
using KeyboardMixer.Core.Hotkeys;
using KeyboardMixer.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KeyboardMixer.Views;

/// <summary>The whole settings UI: shortcuts, volume and flyout options, startup, and about.</summary>
public sealed partial class SettingsPage : Page
{
    private const string RepositoryUrl = "https://github.com/troyhayes/KeyboardMixer";

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => ViewModel.Attach();
        Unloaded += (_, _) => ViewModel.Detach();
    }

    public ShortcutsViewModel ViewModel { get; } = new();

    public GeneralViewModel General { get; } = new();

    public string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            int plus = version.IndexOf('+'); // drop the source-revision suffix
            return "Version " + (plus >= 0 ? version[..plus] : version);
        }
    }

    public string SettingsPath => App.Current.Settings.FilePath;

    private void OnOpenSettingsFolderClick(object sender, RoutedEventArgs e)
    {
        var folder = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }

    private void OnSourceClick(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(RepositoryUrl) { UseShellExecute = true });

    private async void OnAddClick(object sender, RoutedEventArgs e)
    {
        // Start with the first common combination that isn't taken yet.
        HotkeyModifiers[] suggestions =
        [
            HotkeyModifiers.Ctrl | HotkeyModifiers.Shift,
            HotkeyModifiers.Ctrl | HotkeyModifiers.Alt,
            HotkeyModifiers.Ctrl | HotkeyModifiers.Win,
            HotkeyModifiers.Alt | HotkeyModifiers.Shift,
            HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift,
        ];
        var binding = new Binding { Target = BindingTarget.Application, ExeName = "placeholder.exe" };
        binding.Modifiers = suggestions.FirstOrDefault(m =>
        {
            binding.Modifiers = m;
            return BindingValidator.Validate(binding, ViewModel.AllBindings).IsValid;
        }, HotkeyModifiers.None);
        binding.ExeName = null;
        var dialog = new ShortcutDialog(binding, ViewModel.AllBindings, isNew: true) { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            ViewModel.Add(dialog.Result);
    }

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is BindingItemViewModel item)
            await EditAsync(item);
    }

    private async void OnEditFocusedClick(object sender, RoutedEventArgs e) => await EditAsync(ViewModel.Focused);

    private async Task EditAsync(BindingItemViewModel item)
    {
        var dialog = new ShortcutDialog(item.Model.Clone(), ViewModel.AllBindings, isNew: false) { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            ViewModel.Replace(dialog.Result);
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not BindingItemViewModel item) return;

        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Delete shortcut?",
            Content = $"The shortcut for {item.Title} will be removed.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            ViewModel.Delete(item);
    }
}
