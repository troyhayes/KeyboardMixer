using CommunityToolkit.Mvvm.ComponentModel;
using KeyboardMixer.Core.Hotkeys;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace KeyboardMixer.ViewModels;

/// <summary>One shortcut card on the Shortcuts page.</summary>
public sealed partial class BindingItemViewModel : ObservableObject
{
    internal static readonly ImageSource GenericAppIcon = new SvgImageSource(new Uri("ms-appx:///Assets/generic-app.svg"));

    private readonly Action _save;

    internal BindingItemViewModel(Binding model, Action save)
    {
        Model = model;
        _save = save;
        Icon = GenericAppIcon;
        _ = LoadIconAsync();
    }

    internal Binding Model { get; }

    public string Title => Model.Describe();

    public string Subtitle => Model.Target == BindingTarget.FocusedApp
        ? "Changes the volume of the app you're currently using."
        : Model.ExeName ?? "";

    /// <summary>Key chips: the modifiers, then "Volume" for the media keys or the custom keys, e.g. "F10 / F9 / F11".</summary>
    public IReadOnlyList<string> Keys => [.. Model.Modifiers.ToKeyNames(), ActionKeysChip];

    public string KeysDescription => Model.DescribeKeys();

    private string ActionKeysChip => Model.UsesMediaKeys
        ? "Volume"
        : string.Join(" / ", Model.GetKeys().Select(k => VirtualKeys.GetName(k.Key)));

    [ObservableProperty]
    private ImageSource _icon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    public bool HasError => Error is not null;

    public bool Enabled
    {
        get => Model.Enabled;
        set
        {
            if (Model.Enabled == value) return;
            Model.Enabled = value;
            OnPropertyChanged();
            _save();
        }
    }

    private async Task LoadIconAsync()
    {
        if (Model.Target != BindingTarget.Application) return;
        var icon = await App.Current.Icons.GetAsync(Model.ExePath);
        if (icon is not null) Icon = icon;
    }
}
