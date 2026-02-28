using System.IO;
using UhkKeymapAutochanger.Core.Models;
using UhkKeymapAutochanger.Core.Settings;

namespace UhkKeymapAutochanger.Core.Services;

public sealed class KeymapRoutingService
{
    private readonly object _sync = new();
    private Dictionary<string, string> _rules = new(StringComparer.OrdinalIgnoreCase);
    private string _defaultKeymap = "DEF";
    private string? _lastAppliedKeymap;

    public KeymapRoutingService(AppConfig config)
    {
        UpdateConfig(config);
    }

    public void UpdateConfig(AppConfig config)
    {
        var validation = SettingsValidator.Validate(config);
        if (!validation.IsValid)
        {
            throw new InvalidDataException($"Invalid config: {string.Join(" | ", validation.Errors)}");
        }

        lock (_sync)
        {
            _defaultKeymap = validation.NormalizedConfig.DefaultKeymap;
            _rules = validation.NormalizedConfig.Rules.ToDictionary(
                rule => rule.ProcessName,
                rule => rule.Keymap,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool ShouldSwitch(string? activeProcessName, out string targetKeymap)
    {
        lock (_sync)
        {
            targetKeymap = ResolveTargetKeymapLocked(activeProcessName);
            return !string.Equals(_lastAppliedKeymap, targetKeymap, StringComparison.OrdinalIgnoreCase);
        }
    }

    public string ResolveTargetKeymap(string? activeProcessName)
    {
        lock (_sync)
        {
            return ResolveTargetKeymapLocked(activeProcessName);
        }
    }

    public void MarkSwitched(string keymapAbbreviation)
    {
        lock (_sync)
        {
            _lastAppliedKeymap = SettingsValidator.NormalizeKeymap(keymapAbbreviation);
        }
    }

    public void ResetState()
    {
        lock (_sync)
        {
            _lastAppliedKeymap = null;
        }
    }

    private string ResolveTargetKeymapLocked(string? activeProcessName)
    {
        var normalizedProcessName = SettingsValidator.NormalizeProcessName(activeProcessName);
        if (!string.IsNullOrWhiteSpace(normalizedProcessName) &&
            _rules.TryGetValue(normalizedProcessName, out var mappedKeymap))
        {
            return mappedKeymap;
        }

        return _defaultKeymap;
    }
}
