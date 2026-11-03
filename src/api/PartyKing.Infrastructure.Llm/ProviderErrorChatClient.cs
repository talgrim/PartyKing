using PartyKing.Application.Ports;
using System.ClientModel;
using Microsoft.Extensions.AI;

namespace PartyKing.Infrastructure.Llm;

/// <summary>
/// Puts the provider's error message into the exception: the SDK reports only "Status: 400", while the body says
/// what was wrong (an unknown model, an unsupported parameter, an exhausted quota).
/// </summary>
internal sealed class ProviderErrorChatClient : DelegatingChatClient
{
    private const int MaxBodyLength = 500;

    public ProviderErrorChatClient(IChatClient innerClient)
        : base(innerClient)
    {
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }
        catch (ClientResultException e)
        {
            var body = e.GetRawResponse()?.Content.ToString() ?? string.Empty;
            body = body.Length > MaxBodyLength ? body[..MaxBodyLength] : body;

            throw new LlmProviderException(e.Status, $"The LLM answered {e.Status}: {body}", e);
        }
    }
}
