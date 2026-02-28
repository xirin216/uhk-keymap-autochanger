namespace UhkKeymapAutochanger.Diagnostics;

internal interface IDebugLogger
{
    bool Enabled { get; }

    void Log(string message);
}
