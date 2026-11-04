using Microsoft.Extensions.AI;

namespace PartyKing.Infrastructure.Llm;

/// <summary>
/// Stands in for the real client when no API key is configured. The OpenAI SDK refuses to build a client with an empty
/// key, and the services that take an <see cref="IChatClient"/> are built with every request of the game controllers,
/// so building the real one would turn each Ringo and Never have I ever request into a 500. Every call fails instead,
/// which the suggestion services answer with their built-in lists, and no request leaves the server.
/// </summary>
internal sealed class UnavailableChatClient : IChatClient
{
    private const string Reason = "No LLM API key is configured (LlmSettings:ApiKey).";

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(Reason);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(Reason);

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
