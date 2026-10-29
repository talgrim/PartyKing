using Microsoft.Extensions.AI;

namespace PartyKing.Application.Tests;

/// <summary>Answers with <see cref="Reply"/>, or throws what <see cref="Failure"/> says, and counts the calls.</summary>
internal sealed class FakeChatClient : IChatClient
{
    public string? Reply { get; set; }

    public Exception? Failure { get; set; }

    public int Calls { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls++;

        return Failure is null
            ? Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Reply)))
            : Task.FromException<ChatResponse>(Failure);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
