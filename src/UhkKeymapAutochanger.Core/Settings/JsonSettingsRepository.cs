using System.Text.Json;
using UhkKeymapAutochanger.Core.Models;

namespace UhkKeymapAutochanger.Core.Settings;

public sealed class JsonSettingsRepository : ISettingsRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public JsonSettingsRepository(string? configFilePath = null)
    {
        ConfigFilePath = configFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UhkKeymapAutochanger",
            "config.json");
    }

    public string ConfigFilePath { get; }

    public AppConfig LoadOrCreate()
    {
        EnsureConfigDirectory();

        if (!File.Exists(ConfigFilePath))
        {
            var defaults = AppConfig.CreateDefault();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            var validation = SettingsValidator.Validate(loaded);

            if (!validation.IsValid)
            {
                BackupInvalidConfigFile("validation");
                var defaults = AppConfig.CreateDefault();
                Save(defaults);
                return defaults;
            }

            PersistNormalizedConfig(json, validation.NormalizedConfig);
            return validation.NormalizedConfig;
        }
        catch (JsonException)
        {
            BackupInvalidConfigFile("json");
            var defaults = AppConfig.CreateDefault();
            Save(defaults);
            return defaults;
        }
        catch (IOException)
        {
            var defaults = AppConfig.CreateDefault();
            Save(defaults);
            return defaults;
        }
    }

    public void Save(AppConfig config)
    {
        var validation = SettingsValidator.Validate(config);
        if (!validation.IsValid)
        {
            throw new InvalidDataException($"Invalid configuration: {string.Join(" | ", validation.Errors)}");
        }

        EnsureConfigDirectory();
        var json = JsonSerializer.Serialize(validation.NormalizedConfig, JsonOptions);
        File.WriteAllText(ConfigFilePath, json);
    }

    private void EnsureConfigDirectory()
    {
        var directory = Path.GetDirectoryName(ConfigFilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Config file path is invalid.");
        }

        Directory.CreateDirectory(directory);
    }

    private void PersistNormalizedConfig(string currentJson, AppConfig normalizedConfig)
    {
        var normalizedJson = JsonSerializer.Serialize(normalizedConfig, JsonOptions);
        if (!string.Equals(currentJson.Trim(), normalizedJson, StringComparison.Ordinal))
        {
            File.WriteAllText(ConfigFilePath, normalizedJson);
        }
    }

    private void BackupInvalidConfigFile(string reason)
    {
        if (!File.Exists(ConfigFilePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(ConfigFilePath)!;
        var backupName = $"{Path.GetFileNameWithoutExtension(ConfigFilePath)}.{reason}.{DateTime.UtcNow:yyyyMMddHHmmss}.invalid.json";
        var backupPath = Path.Combine(directory, backupName);
        File.Copy(ConfigFilePath, backupPath, overwrite: true);
    }
}
