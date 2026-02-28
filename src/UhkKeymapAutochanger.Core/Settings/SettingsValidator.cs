using System.IO;
using UhkKeymapAutochanger.Core.Models;

namespace UhkKeymapAutochanger.Core.Settings;

public static class SettingsValidator
{
    public const int MinPollIntervalMs = 100;
    public const int MaxPollIntervalMs = 1000;
    public const string DefaultLayer = "base";

    private const int MaxKeymapLength = 255;
    private static readonly string[] SupportedLayersInternal =
    {
        "base",
        "fn",
        "mod",
        "mouse",
        "fn2",
        "fn3",
        "fn4",
        "fn5",
        "alt",
        "shift",
        "super",
        "ctrl",
    };
    private static readonly HashSet<string> SupportedLayerSet = new(SupportedLayersInternal, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> SupportedLayers => SupportedLayersInternal;

    public static SettingsValidationResult Validate(AppConfig? config)
    {
        var errors = new List<string>();
        var normalized = new AppConfig();

        config ??= AppConfig.CreateDefault();

        normalized.DefaultKeymap = NormalizeKeymap(config.DefaultKeymap);
        if (string.IsNullOrWhiteSpace(normalized.DefaultKeymap))
        {
            errors.Add("defaultKeymap is required.");
        }
        else if (normalized.DefaultKeymap.Length > MaxKeymapLength)
        {
            errors.Add($"defaultKeymap must be <= {MaxKeymapLength} characters.");
        }
        else if (!IsAscii(normalized.DefaultKeymap))
        {
            errors.Add("defaultKeymap must be ASCII only.");
        }

        normalized.PollIntervalMs = config.PollIntervalMs;
        if (normalized.PollIntervalMs < MinPollIntervalMs || normalized.PollIntervalMs > MaxPollIntervalMs)
        {
            errors.Add($"pollIntervalMs must be between {MinPollIntervalMs} and {MaxPollIntervalMs}.");
        }

        normalized.StartWithWindows = config.StartWithWindows;
        normalized.PauseWhenUhkAgentRunning = config.PauseWhenUhkAgentRunning;
        normalized.Rules = new List<ProcessRule>();

        var seenProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rules = config.Rules ?? new List<ProcessRule>();

        for (var i = 0; i < rules.Count; i++)
        {
            ProcessRule? rule = rules[i];
            if (rule is null)
            {
                errors.Add($"rules[{i}] cannot be null.");
                continue;
            }

            var processName = NormalizeProcessName(rule.ProcessName);
            var keymap = NormalizeKeymap(rule.Keymap);
            var layer = NormalizeLayer(rule.Layer);
            if (string.IsNullOrWhiteSpace(layer))
            {
                layer = DefaultLayer;
            }

            if (string.IsNullOrWhiteSpace(processName) &&
                string.IsNullOrWhiteSpace(keymap) &&
                string.Equals(layer, DefaultLayer, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(processName))
            {
                errors.Add($"rules[{i}].processName is required.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(keymap))
            {
                errors.Add($"rules[{i}].keymap is required.");
                continue;
            }

            if (keymap.Length > MaxKeymapLength)
            {
                errors.Add($"rules[{i}].keymap must be <= {MaxKeymapLength} characters.");
                continue;
            }

            if (!IsAscii(keymap))
            {
                errors.Add($"rules[{i}].keymap must be ASCII only.");
                continue;
            }

            if (!SupportedLayerSet.Contains(layer))
            {
                errors.Add($"rules[{i}].layer must be one of: {string.Join(", ", SupportedLayersInternal)}.");
                continue;
            }

            if (!seenProcesses.Add(processName))
            {
                errors.Add($"Duplicate process rule found for '{processName}'.");
                continue;
            }

            normalized.Rules.Add(new ProcessRule
            {
                ProcessName = processName,
                Keymap = keymap,
                Layer = layer,
            });
        }

        return new SettingsValidationResult(normalized, errors);
    }

    public static string NormalizeProcessName(string? processName)
    {
        var normalized = (processName ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        normalized = Path.GetFileName(normalized);
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        if (!normalized.Contains('.'))
        {
            normalized += ".exe";
        }

        return normalized;
    }

    public static string NormalizeKeymap(string? keymap)
    {
        return (keymap ?? string.Empty).Trim().ToUpperInvariant();
    }

    public static string NormalizeLayer(string? layer)
    {
        return (layer ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static bool IsAscii(string value)
    {
        return value.All(ch => ch <= sbyte.MaxValue);
    }
}
