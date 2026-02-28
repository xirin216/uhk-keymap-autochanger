using UhkKeymapAutochanger.Core.Models;
using UhkKeymapAutochanger.Core.Settings;

namespace UhkKeymapAutochanger.Tests;

public sealed class SettingsValidatorTests
{
    [Fact]
    public void ShouldRejectEmptyDefaultKeymap()
    {
        var config = new AppConfig
        {
            DefaultKeymap = " ",
            PollIntervalMs = 250,
            StartWithWindows = true,
            Rules = new List<ProcessRule>(),
        };

        var result = SettingsValidator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("defaultKeymap", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(1001)]
    public void ShouldRejectOutOfRangePollInterval(int interval)
    {
        var config = new AppConfig
        {
            DefaultKeymap = "DEF",
            PollIntervalMs = interval,
            StartWithWindows = true,
            Rules = new List<ProcessRule>(),
        };

        var result = SettingsValidator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("pollIntervalMs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ShouldRejectDuplicateProcessRulesIgnoringCase()
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
                new()
                {
                    ProcessName = "code.EXE",
                    Keymap = "DEV2",
                },
            },
        };

        var result = SettingsValidator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ShouldNormalizeProcessNamesAndKeymaps()
    {
        var config = new AppConfig
        {
            DefaultKeymap = " def ",
            PollIntervalMs = 250,
            StartWithWindows = true,
            Rules = new List<ProcessRule>
            {
                new()
                {
                    ProcessName = " Code ",
                    Keymap = " dev ",
                },
            },
        };

        var result = SettingsValidator.Validate(config);

        Assert.True(result.IsValid);
        Assert.Equal("DEF", result.NormalizedConfig.DefaultKeymap);
        Assert.Single(result.NormalizedConfig.Rules);
        Assert.Equal("Code.exe", result.NormalizedConfig.Rules[0].ProcessName);
        Assert.Equal("DEV", result.NormalizedConfig.Rules[0].Keymap);
    }
}
