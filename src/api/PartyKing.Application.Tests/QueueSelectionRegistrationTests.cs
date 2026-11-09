using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Playlists;

namespace PartyKing.Application.Tests;

/// <summary>The registrations in Application's DI must cover every section of the queue, or saving would quietly miss songs.</summary>
public class QueueSelectionRegistrationTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.RegisterApplication(new ConfigurationBuilder().Build());

        return services.BuildServiceProvider();
    }

    [Fact]
    public void EverySectionOfTheQueue_HasExactlyOneRegisteredSpecification()
    {
        using var provider = Build();

        var specifications = provider.GetServices<IQueueSectionSpecification>().ToList();

        foreach (var section in Enum.GetValues<QueueSection>())
        {
            Assert.Single(specifications, x => x.Section == section);
        }
    }

    [Fact]
    public void TheSelection_CanBeBuiltFromTheRegistrations_AndIsShared()
    {
        using var provider = Build();

        Assert.IsType<QueueSelection>(provider.GetRequiredService<IQueueSelection>());
        Assert.Same(provider.GetRequiredService<IQueueSelection>(), provider.GetRequiredService<IQueueSelection>());
    }
}
