namespace UhkKeymapAutochanger.Core.Models;

public sealed class AppConfig
{
    public string DefaultKeymap { get; set; } = "DEF";

    public int PollIntervalMs { get; set; } = 250;

    public bool StartWithWindows { get; set; } = true;

    public bool PauseWhenUhkAgentRunning { get; set; } = true;

    public List<ProcessRule> Rules { get; set; } = new();

    public static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            DefaultKeymap = "DEF",
            PollIntervalMs = 250,
            StartWithWindows = true,
            PauseWhenUhkAgentRunning = true,
            Rules = new List<ProcessRule>(),
        };
    }
}
