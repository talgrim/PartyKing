using PartyKing.Application.Ports;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace PartyKing.Infrastructure.Llm;

/// <summary>
/// Asks a second model when the first is overloaded or too slow (a free-tier model often answers 503 at busy times).
/// A refused request (400, 401, 404) is not retried: another model would refuse it too.
/// </summary>
internal sealed class FallbackChatClient : DelegatingChatClient
{
    private readonly IChatClient _fallback;
    private readonly ILogger _logger;

    public FallbackChatClient(IChatClient primary, IChatClient fallback, ILogger logger)
        : base(primary)
    {
        _fallback = fallback;
        _logger = logger;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var chatMessages = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();

        try
        {
            return await base.GetResponseAsync(chatMessages, options, cancellationToken);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested && IsWorthAnotherModel(e))
        {
            _logger.LogWarning(e, "The LLM is unavailable, asking the fallback model");

            // The options may hold settings meant for the first model (its reasoning effort), so the fallback gets none
            return await _fallback.GetResponseAsync(chatMessages, cancellationToken: cancellationToken);
        }
    }

    private static bool IsWorthAnotherModel(Exception e) =>
        e is TimeoutException or TaskCanceledException or AggregateException
        || e is LlmProviderException { IsTransient: true }
        || e.InnerException is not null && IsWorthAnotherModel(e.InnerException);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fallback.Dispose();
        }

        base.Dispose(disposing);
    }
}
