using KeyboardMixer.Core.Processes;

namespace KeyboardMixer.Core.Hotkeys;

public sealed record BindingValidationResult(bool IsValid, string? Error)
{
    public static readonly BindingValidationResult Valid = new(true, null);
    public static BindingValidationResult Invalid(string error) => new(false, error);
}

public static class BindingValidator
{
    /// <summary>
    /// Checks a new or edited binding against the existing ones. <paramref name="existing"/> may
    /// include the binding being edited (matched by Id); it is ignored for conflict checks.
    /// </summary>
    public static BindingValidationResult Validate(Binding candidate, IEnumerable<Binding> existing)
    {
        if (ValidateKeys(candidate) is string keyError)
            return BindingValidationResult.Invalid(keyError);

        var keys = candidate.GetKeys().ToList();

        if (candidate.Target == BindingTarget.Application)
        {
            if (string.IsNullOrWhiteSpace(candidate.ExeName))
                return BindingValidationResult.Invalid("Choose an app.");

            if (!ExeName.IsValid(candidate.ExeName))
                return BindingValidationResult.Invalid("Enter a valid program name, like spotify.exe.");
        }

        foreach (var other in existing)
        {
            if (other.Id == candidate.Id || other.Modifiers != candidate.Modifiers) continue;
            var taken = other.GetKeys().Select(k => k.Key).ToHashSet();
            var clash = keys.FirstOrDefault(k => taken.Contains(k.Key));
            if (clash != default)
                return BindingValidationResult.Invalid(
                    $"{Combo(candidate.Modifiers, clash.Key)} is already used by \"{other.Describe()}\".");
        }

        return BindingValidationResult.Valid;
    }

    /// <summary>Checks the modifier and key choice on its own. Returns an error message, or null when it can be registered.</summary>
    public static string? ValidateKeys(Binding binding)
    {
        var keys = binding.GetKeys().ToList();

        if (keys.Any(k => !VirtualKeys.IsAllowed(k.Key)))
            return "Choose a key for volume up and volume down.";

        if (keys.Select(k => k.Key).Distinct().Count() != keys.Count)
            return "Use a different key for each action.";

        // Without modifiers the shortcut would replace normal typing or the system volume keys.
        if (binding.Modifiers == HotkeyModifiers.None && !keys.All(k => VirtualKeys.IsSafeWithoutModifiers(k.Key)))
            return "Choose at least one modifier key. Only F13–F24 can be used on their own.";

        return null;
    }

    public static string Combo(HotkeyModifiers modifiers, int key) =>
        modifiers == HotkeyModifiers.None
            ? VirtualKeys.GetName(key)
            : $"{modifiers.ToDisplayString()} + {VirtualKeys.GetName(key)}";
}
