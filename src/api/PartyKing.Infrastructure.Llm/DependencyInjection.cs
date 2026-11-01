using PartyKing.Application.Configuration;
using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

// The reasoning effort setting of the OpenAI SDK is still marked as experimental
#pragma warning disable OPENAI001

namespace PartyKing.Infrastructure.Llm;

public static class DependencyInjection
{
    public static void RegisterLlmInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmConfiguration>(configuration.GetSection(LlmConfiguration.SectionName));

        services
            .AddChatClient(provider =>
            {
                var options = provider.GetRequiredService<IOptions<LlmConfiguration>>().Value;

                var client = new OpenAIClient(
                    new ApiKeyCredential(options.ApiKey ?? string.Empty),
                    new OpenAIClientOptions
                    {
                        Endpoint = new Uri(options.BaseUrl),
                        NetworkTimeout = TimeSpan.FromSeconds(30),

                        // The default is 4 tries, which would keep the host waiting minutes; a failure falls back to the built-in list
                        RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
                    });

                var primary = CreateChatClient(client, options.Model, options.ReasoningEffort);

                return string.IsNullOrWhiteSpace(options.FallbackModel)
                    ? primary
                    : new FallbackChatClient(
                        primary,
                        CreateChatClient(client, options.FallbackModel, reasoningEffort: null),
                        provider.GetRequiredService<ILoggerFactory>().CreateLogger<FallbackChatClient>());
            })
            .UseLogging()
            .UseOpenTelemetry();
    }

    private static IChatClient CreateChatClient(OpenAIClient client, string model, string? reasoningEffort)
    {
        var builder = new ChatClientBuilder(client.GetChatClient(model).AsIChatClient())
            .Use(inner => new ProviderErrorChatClient(inner));

        if (!string.IsNullOrWhiteSpace(reasoningEffort))
        {
            builder.ConfigureOptions(options =>
                options.RawRepresentationFactory = _ => new ChatCompletionOptions
                {
                    ReasoningEffortLevel = new ChatReasoningEffortLevel(reasoningEffort),
                });
        }

        return builder.Build();
    }
}
