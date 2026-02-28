using System.Diagnostics;
using System.Runtime.InteropServices;
using UhkKeymapAutochanger.Core.Settings;

namespace UhkKeymapAutochanger.Services;

internal static class ForegroundWindowHelper
{
    public static string? GetActiveProcessName()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        _ = GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return SettingsValidator.NormalizeProcessName(process.ProcessName);
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
