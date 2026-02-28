using System.IO;
using UhkKeymapAutochanger.Core.Models;

namespace UhkKeymapAutochanger.Core.Settings;

public static class SettingsValidator
{
    public const int MinPollIntervalMs = 100;
    public const int MaxPollIntervalMs = 1000;

    private const int MaxKeymapLength = 255;

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

            if (string.IsNullOrWhiteSpace(processName) && string.IsNullOrWhiteSpace(keymap))
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

            if (!seenProcesses.Add(processName))
            {
                errors.Add($"Duplicate process rule found for '{processName}'.");
                continue;
            }

            normalized.Rules.Add(new ProcessRule
            {
                ProcessName = processName,
                Keymap = keymap,
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
}
