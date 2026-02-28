using UhkKeymapAutochanger.Core.Models;
using UhkKeymapAutochanger.Core.Services;

namespace UhkKeymapAutochanger.Tests;

public sealed class KeymapRoutingServiceTests
{
    [Fact]
    public void ShouldResolveMappedProcessCaseInsensitively()
    {
        var config = new AppConfig
        {
            DefaultKeymap = "DEF",
            PollIntervalMs = 250,
            StartWithWindows = true,
            Rules = new List<ProcessRule>
            {
                new()
                {
                    ProcessName = "Code.exe",
                    Keymap = "dev",
                },
            },
        };

        var routingService = new KeymapRoutingService(config);

        var shouldSwitch = routingService.ShouldSwitch("code.exe", out var keymap);

        Assert.True(shouldSwitch);
        Assert.Equal("DEV", keymap);
    }

    [Fact]
    public void ShouldFallbackToDefaultKeymapForUnknownProcess()
    {
        var config = new AppConfig
        {
            DefaultKeymap = "DEF",
            PollIntervalMs = 250,
            StartWithWindows = true,
            Rules = new List<ProcessRule>
            {
                new()
                {
                    ProcessName = "Code.exe",
                    Keymap = "DEV",
                },
            },
        };

        var routingService = new KeymapRoutingService(config);

        var shouldSwitch = routingService.ShouldSwitch("chrome.exe", out var keymap);

        Assert.True(shouldSwitch);
        Assert.Equal("DEF", keymap);
    }

    [Fact]
    public void ShouldSuppressDuplicateKeymapSwitches()
    {
        var config = new AppConfig
        {
            DefaultKeymap = "DEF",
            PollIntervalMs = 250,
            StartWithWindows = true,
            Rules = new List<ProcessRule>
            {
                new()
                {
                    ProcessName = "Code.exe",
                    Keymap = "DEV",
                },
            },
        };

        var routingService = new KeymapRoutingService(config);

        Assert.True(routingService.ShouldSwitch("Code.exe", out var keymap));
        Assert.Equal("DEV", keymap);

        routingService.MarkSwitched(keymap);

        Assert.False(routingService.ShouldSwitch("code.exe", out _));
    }
}
