using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Demo;

namespace PartyKing.Application.Demo.Services;

/// <summary>Whether this server is a demo: the one place the features outside the demo ask.</summary>
public interface IDemoMode
{
    bool IsEnabled { get; }

    /// <summary>What the landing page asks before it shows anything.</summary>
    DemoConfigDto GetConfig();
}

internal class DemoMode : IDemoMode
{
    private readonly DemoConfiguration _configuration;
    private readonly IDateTimeProvider _clock;

    public DemoMode(IOptions<DemoConfiguration> options, IDateTimeProvider clock)
    {
        _configuration = options.Value;
        _clock = clock;
    }

    public bool IsEnabled => _configuration.Enabled;

    public DemoConfigDto GetConfig()
    {
        return new DemoConfigDto(IsEnabled, _clock.UtcNow);
    }
}
