using System.Text.Json;
using ErrorOr;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Games;
using PartyKing.Infrastructure.Llm;

namespace PartyKing.Application.Games.Services;

public interface INeverHaveIEverSuggestionService
{
    /// <summary>
    /// Host or admin: a few spicy "Never have I ever" statements in Polish. Falls back to a built-in list when the
    /// language model is unavailable, so it never fails for that reason. Forbidden for anyone else.
    /// </summary>
    Task<ErrorOr<GameSuggestionsDto>> SuggestAsync(
        Guid sessionId,
        string? profileId,
        bool isAdmin,
        CancellationToken cancellationToken);
}

internal class NeverHaveIEverSuggestionService : INeverHaveIEverSuggestionService
{
    internal const int Count = 5;
    internal const int MaxLength = 200;

    private const string SystemPrompt =
        "Jesteś prowadzącym imprezę dla dorosłych (18+). Piszesz zdania do gry „Nigdy w życiu” po polsku. " +
        "Zdania mają być pikantne, odważne i zabawne (flirt, imprezy, alkohol, wpadki, randki), ale bez " +
        "przemocy, przestępstw, nienawiści, osób nieletnich i bez drastycznych szczegółów. " +
        "Każde zdanie zaczyna się od słów „Nigdy w życiu nie”. " +
        "Odpowiedz wyłącznie tablicą JSON z samymi napisami, bez żadnego dodatkowego tekstu.";

    private static readonly string UserPrompt =
        $"Podaj {Count} różnych zdań. Seed: {{0}}.";

    // Shown when the model can't be reached, so the host always gets something to read out
    private static readonly IReadOnlyList<string> Fallback =
    [
        "Nigdy w życiu nie pocałowałem(-am) kogoś, kogo imienia nie znałem(-am).",
        "Nigdy w życiu nie wysłałem(-am) wiadomości do byłej/byłego po alkoholu.",
        "Nigdy w życiu nie udawałem(-am), że śpię, żeby uniknąć rozmowy.",
        "Nigdy w życiu nie zakochałem(-am) się w kimś z imprezy.",
        "Nigdy w życiu nie wymknąłem(-am) się z randki przez okno w toalecie.",
        "Nigdy w życiu nie obudziłem(-am) się w miejscu, którego nie rozpoznawałem(-am).",
    ];

    private readonly ISessionService _sessionService;
    private readonly IChatClient _chatClient;
    private readonly LlmConfiguration _configuration;
    private readonly ILogger<NeverHaveIEverSuggestionService> _logger;

    public NeverHaveIEverSuggestionService(
        ISessionService sessionService,
        IChatClient chatClient,
        IOptions<LlmConfiguration> configuration,
        ILogger<NeverHaveIEverSuggestionService> logger)
    {
        _sessionService = sessionService;
        _chatClient = chatClient;
        _configuration = configuration.Value;
        _logger = logger;
    }

    public async Task<ErrorOr<GameSuggestionsDto>> SuggestAsync(
        Guid sessionId,
        string? profileId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        if (await _sessionService.AuthorizeManagerAsync(sessionId, profileId, isAdmin, cancellationToken)
            is { IsError: true } denied)
        {
            return denied.Errors;
        }

        var suggestions = Parse(await AskAsync(cancellationToken));

        return suggestions.Count > 0
            ? new GameSuggestionsDto(suggestions, FromFallback: false)
            : new GameSuggestionsDto(Fallback.OrderBy(_ => Random.Shared.Next()).Take(Count).ToList(), FromFallback: true);
    }

    /// <summary>The model's raw answer; null when there is no key or the model could not be reached (quota, timeout, outage).</summary>
    private async Task<string?> AskAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_configuration.ApiKey))
        {
            return null;
        }

        try
        {
            // The seed keeps repeated requests from getting the same answer out of the model
            var response = await _chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, SystemPrompt),
                    new ChatMessage(ChatRole.User, string.Format(UserPrompt, Random.Shared.Next())),
                ],
                cancellationToken: cancellationToken);

            return response.Text;
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(e, "Could not get statements from the LLM");
            return null;
        }
    }

    /// <summary>
    /// Reads a JSON array of strings, tolerating a markdown fence around it. The text is untrusted model output, so
    /// anything that is not a short plain string is dropped, and the count is capped.
    /// </summary>
    internal static IReadOnlyList<string> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return [];
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<string?>>(text[start..(end + 1)]) ?? [];

            return items
                .Select(item => item?.Trim())
                .Where(item => !string.IsNullOrEmpty(item) && item.Length <= MaxLength)
                .Select(item => item!)
                .Distinct()
                .Take(Count)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
