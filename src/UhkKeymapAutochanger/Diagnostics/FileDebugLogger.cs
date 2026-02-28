namespace UhkKeymapAutochanger.Diagnostics;

internal sealed class FileDebugLogger : IDebugLogger, IDisposable
{
    private readonly object _sync = new();
    private readonly string _logPath;

    public FileDebugLogger(bool enabled)
    {
        Enabled = enabled;
        var appDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UhkKeymapAutochanger");

        _logPath = Path.Combine(appDir, "debug.log");
        if (Enabled)
        {
            Directory.CreateDirectory(appDir);
        }
    }

    public bool Enabled { get; }

    public void Log(string message)
    {
        if (!Enabled)
        {
            return;
        }

        lock (_sync)
        {
            File.AppendAllText(
                _logPath,
                $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        }
    }

    public void Dispose()
    {
    }
}
