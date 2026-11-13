using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Demo.Services;

namespace PartyKing.Application.Tests;

public class DemoModeTests
{
    private readonly FakeClock _clock = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Config_SaysWhetherTheServerIsADemo_WithItsClock(bool enabled)
    {
        var mode = new DemoMode(Options.Create(new DemoConfiguration { Enabled = enabled }), _clock);

        var config = mode.GetConfig();

        Assert.Equal(enabled, mode.IsEnabled);
        Assert.Equal(enabled, config.Enabled);
        Assert.Equal(_clock.UtcNow, config.ServerTime);
    }
}
