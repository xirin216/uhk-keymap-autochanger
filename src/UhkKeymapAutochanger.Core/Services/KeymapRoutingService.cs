using System.IO;
using UhkKeymapAutochanger.Core.Models;
using UhkKeymapAutochanger.Core.Settings;

namespace UhkKeymapAutochanger.Core.Services;

public sealed class KeymapRoutingService
{
    private readonly object _sync = new();
    private Dictionary<string, KeymapLayerTarget> _rules = new(StringComparer.OrdinalIgnoreCase);
    private KeymapLayerTarget _defaultTarget = new("DEF", SettingsValidator.DefaultLayer);
    private KeymapLayerTarget? _lastAppliedTarget;

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
            _defaultTarget = new KeymapLayerTarget(
                validation.NormalizedConfig.DefaultKeymap,
                SettingsValidator.DefaultLayer);
            _rules = validation.NormalizedConfig.Rules.ToDictionary(
                rule => rule.ProcessName,
                rule => new KeymapLayerTarget(rule.Keymap, rule.Layer),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool ShouldSwitch(string? activeProcessName, out KeymapLayerTarget target)
    {
        lock (_sync)
        {
            target = ResolveTargetLocked(activeProcessName);
            return !Equals(_lastAppliedTarget, target);
        }
    }

    public KeymapLayerTarget ResolveTarget(string? activeProcessName)
    {
        lock (_sync)
        {
            return ResolveTargetLocked(activeProcessName);
        }
    }

    public void MarkSwitched(KeymapLayerTarget target)
    {
        lock (_sync)
        {
            _lastAppliedTarget = NormalizeTarget(target);
        }
    }

    public void ResetState()
    {
        lock (_sync)
        {
            _lastAppliedTarget = null;
        }
    }

    private KeymapLayerTarget ResolveTargetLocked(string? activeProcessName)
    {
        var normalizedProcessName = SettingsValidator.NormalizeProcessName(activeProcessName);
        if (!string.IsNullOrWhiteSpace(normalizedProcessName) &&
            _rules.TryGetValue(normalizedProcessName, out var mappedTarget))
        {
            return mappedTarget;
        }

        return _defaultTarget;
    }

    private static KeymapLayerTarget NormalizeTarget(KeymapLayerTarget target)
    {
        var layer = SettingsValidator.NormalizeLayer(target.Layer);
        if (string.IsNullOrWhiteSpace(layer))
        {
            layer = SettingsValidator.DefaultLayer;
        }

        return new KeymapLayerTarget(
            SettingsValidator.NormalizeKeymap(target.Keymap),
            layer);
    }
}
