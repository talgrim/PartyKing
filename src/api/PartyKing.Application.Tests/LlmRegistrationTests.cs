using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Infrastructure.Llm;

namespace PartyKing.Application.Tests;

public class LlmRegistrationTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.RegisterLlmInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WithoutAnApiKey_TheChatClientStillBuilds_AndFailsWhenAsked(string? apiKey)
    {
        // Every Ringo and Never have I ever request builds the services that take the chat client
        using var provider = Build(new() { ["LlmSettings:ApiKey"] = apiKey });

        var client = provider.GetRequiredService<IChatClient>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));
    }

    [Fact]
    public void WithAnApiKey_TheRealClientIsBuilt()
    {
        using var provider = Build(new() { ["LlmSettings:ApiKey"] = "a-key" });

        Assert.NotNull(provider.GetRequiredService<IChatClient>());
    }
}
