using Microsoft.Win32;
using UhkKeymapAutochanger.Diagnostics;

namespace UhkKeymapAutochanger.Services;

internal sealed class StartupRegistrationService
{
    private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "UhkKeymapAutochanger";

    private readonly IDebugLogger _logger;

    public StartupRegistrationService(IDebugLogger logger)
    {
        _logger = logger;
    }

    public void SetEnabled(bool enabled, string executablePath)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunRegistryPath, writable: true);
        if (runKey is null)
        {
            throw new InvalidOperationException("Failed to open startup registry key.");
        }

        if (enabled)
        {
            runKey.SetValue(ValueName, $"\"{executablePath}\"", RegistryValueKind.String);
            _logger.Log($"Startup registration enabled: {executablePath}");
        }
        else
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
            _logger.Log("Startup registration disabled.");
        }
    }
}
