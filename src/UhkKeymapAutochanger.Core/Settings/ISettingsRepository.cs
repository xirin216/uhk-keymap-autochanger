using UhkKeymapAutochanger.Core.Models;

namespace UhkKeymapAutochanger.Core.Settings;

public interface ISettingsRepository
{
    string ConfigFilePath { get; }

    AppConfig LoadOrCreate();

    void Save(AppConfig config);
}
