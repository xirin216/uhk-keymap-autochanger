using System.Diagnostics;
using UhkKeymapAutochanger.Core.Models;
using UhkKeymapAutochanger.Core.Services;
using UhkKeymapAutochanger.Core.Settings;
using UhkKeymapAutochanger.Diagnostics;

namespace UhkKeymapAutochanger.Services;

internal sealed class KeymapSwitchingService : IDisposable
{
    private readonly ForegroundProcessWatcher _foregroundProcessWatcher;
    private readonly KeymapRoutingService _routingService;
    private readonly IKeymapTransport _transport;
    private readonly IDebugLogger _logger;
    private readonly SemaphoreSlim _switchLock = new(1, 1);
    private volatile bool _pauseWhenUhkAgentRunning;
    private volatile bool _running;

    public KeymapSwitchingService(
        ForegroundProcessWatcher foregroundProcessWatcher,
        KeymapRoutingService routingService,
        IKeymapTransport transport,
        IDebugLogger logger,
        bool pauseWhenUhkAgentRunning)
    {
        _foregroundProcessWatcher = foregroundProcessWatcher;
        _routingService = routingService;
        _transport = transport;
        _logger = logger;
        _pauseWhenUhkAgentRunning = pauseWhenUhkAgentRunning;
        _foregroundProcessWatcher.ForegroundProcessChanged += OnForegroundProcessChanged;
    }

    public void Start()
    {
        if (_running)
        {
            return;
        }

        _routingService.ResetState();
        _running = true;
        _foregroundProcessWatcher.Start();
        _ = TrySwitchForCurrentProcessAsync();
    }

    public void Stop()
    {
        _running = false;
        _foregroundProcessWatcher.Stop();
    }

    public void ApplyConfig(AppConfig config)
    {
        _routingService.UpdateConfig(config);
        _foregroundProcessWatcher.UpdateInterval(config.PollIntervalMs);
        _pauseWhenUhkAgentRunning = config.PauseWhenUhkAgentRunning;
        _routingService.ResetState();

        if (_running)
        {
            _ = TrySwitchForCurrentProcessAsync();
        }
    }

    public void Dispose()
    {
        _foregroundProcessWatcher.ForegroundProcessChanged -= OnForegroundProcessChanged;
        _switchLock.Dispose();
    }

    private async void OnForegroundProcessChanged(object? sender, string processName)
    {
        await TrySwitchAsync(processName);
    }

    private async Task TrySwitchForCurrentProcessAsync()
    {
        var currentProcess = ForegroundWindowHelper.GetActiveProcessName();
        if (!string.IsNullOrWhiteSpace(currentProcess))
        {
            await TrySwitchAsync(currentProcess);
        }
    }

    private async Task TrySwitchAsync(string processName)
    {
        if (!_running)
        {
            return;
        }

        await _switchLock.WaitAsync();
        try
        {
            if (!_running)
            {
                return;
            }

            if (!_routingService.ShouldSwitch(processName, out var targetKeymap))
            {
                return;
            }

            if (_pauseWhenUhkAgentRunning && IsUhkAgentRunning())
            {
                _logger.Log("Skipping keymap switch because UHK Agent is running.");
                return;
            }

            await _transport.SwitchKeymapAsync(targetKeymap.Keymap);

            if (!string.Equals(targetKeymap.Layer, SettingsValidator.DefaultLayer, StringComparison.Ordinal))
            {
                await _transport.ExecuteMacroCommandAsync($"toggleLayer {targetKeymap.Layer}");
            }

            _routingService.MarkSwitched(targetKeymap);
            _logger.Log(
                $"Switched target to keymap='{targetKeymap.Keymap}', layer='{targetKeymap.Layer}' for process '{processName}'.");
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to switch target for process '{processName}': {ex.Message}");
        }
        finally
        {
            _switchLock.Release();
        }
    }

    private static bool IsUhkAgentRunning()
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var normalized = NormalizeProcessName(process.ProcessName);
                if (normalized.Contains("UHKAGENT", StringComparison.Ordinal) ||
                    normalized.Contains("ULTIMATEHACKINGKEYBOARDAGENT", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }

    private static string NormalizeProcessName(string processName)
    {
        var chars = processName.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToUpperInvariant();
    }
}
