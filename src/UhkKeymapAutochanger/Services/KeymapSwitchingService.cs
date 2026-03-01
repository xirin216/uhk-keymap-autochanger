using System.Diagnostics;
using UhkKeymapAutochanger.Core.Models;
using UhkKeymapAutochanger.Core.Services;
using UhkKeymapAutochanger.Core.Settings;
using UhkKeymapAutochanger.Diagnostics;

namespace UhkKeymapAutochanger.Services;

internal sealed class KeymapSwitchingService : IDisposable
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);

    private readonly object _lifecycleSync = new();
    private readonly ForegroundProcessWatcher _foregroundProcessWatcher;
    private readonly KeymapRoutingService _routingService;
    private readonly IKeymapTransport _transport;
    private readonly IDebugLogger _logger;
    private readonly SemaphoreSlim _switchLock = new(1, 1);
    private CancellationTokenSource? _retryCts;
    private Task? _retryTask;
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

    public event EventHandler<string>? StatusChanged;

    public void Start()
    {
        lock (_lifecycleSync)
        {
            if (_running)
            {
                return;
            }

            _routingService.ResetState();
            _running = true;
            _foregroundProcessWatcher.Start();
            _retryCts = new CancellationTokenSource();
            _retryTask = Task.Run(() => RetryLoopAsync(_retryCts.Token));
        }

        PublishStatus("Switching started.");
        _ = TrySwitchForCurrentProcessAsync();
    }

    public void Stop()
    {
        CancellationTokenSource? retryCts;
        Task? retryTask;

        lock (_lifecycleSync)
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            retryCts = _retryCts;
            retryTask = _retryTask;
            _retryCts = null;
            _retryTask = null;
        }

        _foregroundProcessWatcher.Stop();
        if (retryCts is not null)
        {
            retryCts.Cancel();
            try
            {
                retryTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(x => x is TaskCanceledException))
            {
            }
            finally
            {
                retryCts.Dispose();
            }
        }

        PublishStatus("Switching stopped.");
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

        PublishStatus("Config applied.");
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
                PublishStatus("Paused because UHK Agent is running.");
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
            PublishStatus(
                $"Applied keymap='{targetKeymap.Keymap}', layer='{targetKeymap.Layer}' for '{processName}'.");
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to switch target for process '{processName}': {ex.Message}");
            PublishStatus($"Switch failed for '{processName}': {ex.Message}");
        }
        finally
        {
            _switchLock.Release();
        }
    }

    private async Task RetryLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RetryInterval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            await TrySwitchForCurrentProcessAsync();
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

    private void PublishStatus(string message)
    {
        StatusChanged?.Invoke(this, message);
    }
}
