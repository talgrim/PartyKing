using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Infrastructure.Llm;

namespace PartyKing.Application.Tests;

public class FallbackChatClientTests
{
    private readonly FakeChatClient _primary = new();
    private readonly FakeChatClient _fallback = new() { Reply = "from the fallback" };
    private readonly FallbackChatClient _client;

    public FallbackChatClientTests()
    {
        _client = new FallbackChatClient(_primary, _fallback, NullLogger.Instance);
    }

    private Task<ChatResponse> AskAsync(CancellationToken cancellationToken = default) =>
        _client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], cancellationToken: cancellationToken);

    [Fact]
    public async Task ASuccessfulPrimary_IsUsedAlone()
    {
        _primary.Reply = "from the primary";

        Assert.Equal("from the primary", (await AskAsync()).Text);
        Assert.Equal(0, _fallback.Calls);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task AnOverloadedPrimary_IsReplacedByTheFallback(int status)
    {
        _primary.Failure = new LlmProviderException(status, "busy", new Exception());

        Assert.Equal("from the fallback", (await AskAsync()).Text);
    }

    [Fact]
    public async Task ATimedOutPrimary_IsReplacedByTheFallback()
    {
        _primary.Failure = new TaskCanceledException("timeout");

        Assert.Equal("from the fallback", (await AskAsync()).Text);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(404)]
    public async Task ARefusedRequest_IsNotRetriedWithTheFallback(int status)
    {
        _primary.Failure = new LlmProviderException(status, "refused", new Exception());

        await Assert.ThrowsAsync<LlmProviderException>(() => AskAsync());
        Assert.Equal(0, _fallback.Calls);
    }

    [Fact]
    public async Task ACancelledCaller_DoesNotTriggerTheFallback()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _primary.Failure = new TaskCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AskAsync(cancellation.Token));
        Assert.Equal(0, _fallback.Calls);
    }
}
