using Microsoft.Extensions.Configuration;

namespace PartyKing.Application.Configuration;

public static class DemoConfigurationExtensions
{
    /// <summary>
    /// The demo settings as configured, for the code that runs before the service provider exists (the services to
    /// register, the rate limits). Everything that runs later reads <see cref="Demo.Services.IDemoMode"/>.
    /// </summary>
    public static DemoConfiguration GetDemoConfiguration(this IConfiguration configuration)
    {
        return configuration.GetSection(DemoConfiguration.SectionName).Get<DemoConfiguration>() ?? new DemoConfiguration();
    }
}
