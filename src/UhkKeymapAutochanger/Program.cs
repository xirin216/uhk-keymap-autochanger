using UhkKeymapAutochanger.Core.Settings;
using UhkKeymapAutochanger.Diagnostics;

namespace UhkKeymapAutochanger;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var debugMode = args.Any(arg => string.Equals(arg, "--debug", StringComparison.OrdinalIgnoreCase));
        using var logger = new FileDebugLogger(debugMode);

        try
        {
            var settingsRepository = new JsonSettingsRepository();
            var config = settingsRepository.LoadOrCreate();
            Application.Run(new TrayApplicationContext(config, settingsRepository, logger));
        }
        catch (Exception ex)
        {
            logger.Log($"Fatal startup error: {ex}");
            MessageBox.Show(
                $"Application failed to start.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "UHK Keymap Autochanger",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
