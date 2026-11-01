using PartyKing.Application.Configuration;
using ErrorOr;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PartyKing.Application.Localization;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Sessions;
using PartyKing.Contract.V1.Games;

namespace PartyKing.Application.Games.Services.NeverHaveIEver;

public interface INeverHaveIEverSuggestionService
{
    /// <summary>
    /// Host or admin: a few spicy "Never have I ever" statements in <paramref name="language"/>. Falls back to a built-in list when the
    /// language model is unavailable, so it never fails for that reason. Forbidden for anyone else.
    /// </summary>
    Task<ErrorOr<GameSuggestionsDto>> SuggestAsync(
        Guid sessionId,
        SessionCaller caller,
        UiLanguage language,
        CancellationToken cancellationToken);
}

internal class NeverHaveIEverSuggestionService : INeverHaveIEverSuggestionService
{
    internal const int Count = 5;
    internal const int MaxLength = 200;

    /// <summary>Everything the model and the fallback need in one language.</summary>
    private sealed record Texts(string SystemPrompt, string UserPrompt, IReadOnlyList<string> Fallback);

    // Shown when the model can't be reached, so the host always gets something to read out
    private static readonly Texts Polish = new(
        "Jesteś prowadzącym imprezę dla dorosłych (18+). Piszesz zdania do gry „Nigdy w życiu” po polsku. " +
        "Zdania mają być pikantne, odważne i zabawne (flirt, imprezy, alkohol, wpadki, randki), ale bez " +
        "przemocy, przestępstw, nienawiści, osób nieletnich i bez drastycznych szczegółów. " +
        "Każde zdanie zaczyna się od słów „Nigdy w życiu nie”. " +
        "Odpowiedz wyłącznie tablicą JSON z samymi napisami, bez żadnego dodatkowego tekstu.",
        $"Podaj {Count} różnych zdań. Seed: {{0}}.",
        [
            "Nigdy w życiu nie pocałowałem(-am) kogoś, kogo imienia nie znałem(-am).",
            "Nigdy w życiu nie wysłałem(-am) wiadomości do byłej/byłego po alkoholu.",
            "Nigdy w życiu nie udawałem(-am), że śpię, żeby uniknąć rozmowy.",
            "Nigdy w życiu nie zakochałem(-am) się w kimś z imprezy.",
            "Nigdy w życiu nie wymknąłem(-am) się z randki przez okno w toalecie.",
            "Nigdy w życiu nie obudziłem(-am) się w miejscu, którego nie rozpoznawałem(-am).",
        ]);

    private static readonly Texts English = new(
        "You host a party for adults (18+). You write statements for the game \"Never have I ever\" in English. " +
        "The statements should be spicy, bold and funny (flirting, parties, alcohol, mishaps, dating), but without " +
        "violence, crimes, hatred, minors or graphic details. " +
        "Every statement starts with the words \"Never have I ever\". " +
        "Reply only with a JSON array of strings, without any additional text.",
        $"Give {Count} different statements. Seed: {{0}}.",
        [
            "Never have I ever kissed someone whose name I didn't know.",
            "Never have I ever texted my ex after a few drinks.",
            "Never have I ever pretended to be asleep to avoid a conversation.",
            "Never have I ever fallen in love with someone I met at a party.",
            "Never have I ever sneaked out of a date through the bathroom window.",
            "Never have I ever woken up somewhere I didn't recognise.",
        ]);

    private readonly ISessionAuthorizer _sessionAuthorizer;
    private readonly IChatClient _chatClient;
    private readonly LlmConfiguration _configuration;
    private readonly ILogger<NeverHaveIEverSuggestionService> _logger;

    public NeverHaveIEverSuggestionService(
        ISessionAuthorizer sessionAuthorizer,
        IChatClient chatClient,
        IOptions<LlmConfiguration> configuration,
        ILogger<NeverHaveIEverSuggestionService> logger)
    {
        _sessionAuthorizer = sessionAuthorizer;
        _chatClient = chatClient;
        _configuration = configuration.Value;
        _logger = logger;
    }

    public async Task<ErrorOr<GameSuggestionsDto>> SuggestAsync(
        Guid sessionId,
        SessionCaller caller,
        UiLanguage language,
        CancellationToken cancellationToken)
    {
        if (await _sessionAuthorizer.AuthorizeManagerAsync(sessionId, caller, cancellationToken)
            is { IsError: true } denied)
        {
            return denied.Errors;
        }

        var texts = language == UiLanguage.Pl ? Polish : English;
        var suggestions = Parse(await AskAsync(texts, cancellationToken));

        return suggestions.Count > 0
            ? new GameSuggestionsDto(suggestions, FromFallback: false)
            : new GameSuggestionsDto(texts.Fallback.OrderBy(_ => Random.Shared.Next()).Take(Count).ToList(), FromFallback: true);
    }

    /// <summary>The model's raw answer; null when there is no key or the model could not be reached (quota, timeout, outage).</summary>
    private async Task<string?> AskAsync(Texts texts, CancellationToken cancellationToken)
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
                    new ChatMessage(ChatRole.System, texts.SystemPrompt),
                    new ChatMessage(ChatRole.User, string.Format(texts.UserPrompt, Random.Shared.Next())),
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

    internal static IReadOnlyList<string> Parse(string? text) => SuggestionParser.Parse(text, Count, MaxLength);
}
