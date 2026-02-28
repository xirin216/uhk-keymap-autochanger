using UhkKeymapAutochanger.Core.Settings;

namespace UhkKeymapAutochanger.Services;

internal sealed class ForegroundProcessWatcher : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private int _pollIntervalMs;
    private string? _lastProcessName;

    public ForegroundProcessWatcher(int pollIntervalMs)
    {
        _pollIntervalMs = ClampPollInterval(pollIntervalMs);
    }

    public event EventHandler<string>? ForegroundProcessChanged;

    public void Start()
    {
        lock (_sync)
        {
            if (_cts is not null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => PollLoopAsync(_cts.Token));
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Task? loopTask;

        lock (_sync)
        {
            cts = _cts;
            loopTask = _loopTask;
            _cts = null;
            _loopTask = null;
            _lastProcessName = null;
        }

        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        try
        {
            loopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(x => x is TaskCanceledException))
        {
        }
        finally
        {
            cts.Dispose();
        }
    }

    public void UpdateInterval(int pollIntervalMs)
    {
        Interlocked.Exchange(ref _pollIntervalMs, ClampPollInterval(pollIntervalMs));
    }

    public void Dispose()
    {
        Stop();
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var activeProcess = ForegroundWindowHelper.GetActiveProcessName();
            if (!string.IsNullOrWhiteSpace(activeProcess) &&
                !string.Equals(activeProcess, _lastProcessName, StringComparison.OrdinalIgnoreCase))
            {
                _lastProcessName = activeProcess;
                ForegroundProcessChanged?.Invoke(this, activeProcess);
            }

            try
            {
                await Task.Delay(Volatile.Read(ref _pollIntervalMs), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private static int ClampPollInterval(int pollIntervalMs)
    {
        return Math.Clamp(
            pollIntervalMs,
            SettingsValidator.MinPollIntervalMs,
            SettingsValidator.MaxPollIntervalMs);
    }
}
