using UhkKeymapAutochanger.Core.Models;
using UhkKeymapAutochanger.Core.Services;
using UhkKeymapAutochanger.Core.Settings;

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
                    Layer = "FN",
                },
            },
        };

        var routingService = new KeymapRoutingService(config);

        var shouldSwitch = routingService.ShouldSwitch("code.exe", out var target);

        Assert.True(shouldSwitch);
        Assert.Equal(new KeymapLayerTarget("DEV", "fn"), target);
    }

    [Fact]
    public void ShouldFallbackToDefaultTargetForUnknownProcess()
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
                    Layer = "fn",
                },
            },
        };

        var routingService = new KeymapRoutingService(config);

        var shouldSwitch = routingService.ShouldSwitch("chrome.exe", out var target);

        Assert.True(shouldSwitch);
        Assert.Equal(new KeymapLayerTarget("DEF", SettingsValidator.DefaultLayer), target);
    }

    [Fact]
    public void ShouldSuppressDuplicateTargetSwitches()
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
                    Layer = "fn",
                },
            },
        };

        var routingService = new KeymapRoutingService(config);

        Assert.True(routingService.ShouldSwitch("Code.exe", out var target));
        Assert.Equal(new KeymapLayerTarget("DEV", "fn"), target);

        routingService.MarkSwitched(target);

        Assert.False(routingService.ShouldSwitch("code.exe", out _));
    }

    [Fact]
    public void ShouldSwitchWhenLayerChangesWithSameKeymap()
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
                    Layer = "fn",
                },
                new()
                {
                    ProcessName = "Chrome.exe",
                    Keymap = "DEV",
                    Layer = "mod",
                },
            },
        };

        var routingService = new KeymapRoutingService(config);

        Assert.True(routingService.ShouldSwitch("Code.exe", out var firstTarget));
        Assert.Equal(new KeymapLayerTarget("DEV", "fn"), firstTarget);
        routingService.MarkSwitched(firstTarget);

        Assert.True(routingService.ShouldSwitch("Chrome.exe", out var secondTarget));
        Assert.Equal(new KeymapLayerTarget("DEV", "mod"), secondTarget);
    }
}
