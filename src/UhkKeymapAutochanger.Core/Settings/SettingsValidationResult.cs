using UhkKeymapAutochanger.Core.Models;

namespace UhkKeymapAutochanger.Core.Settings;

public sealed class SettingsValidationResult
{
    public SettingsValidationResult(AppConfig normalizedConfig, IReadOnlyList<string> errors)
    {
        NormalizedConfig = normalizedConfig;
        Errors = errors;
    }

    public AppConfig NormalizedConfig { get; }

    public IReadOnlyList<string> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}
