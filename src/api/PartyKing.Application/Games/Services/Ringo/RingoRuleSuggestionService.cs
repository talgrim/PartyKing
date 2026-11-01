using PartyKing.Application.Configuration;
using ErrorOr;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PartyKing.Application.Localization;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Sessions;
using PartyKing.Contract.V1.Games;

namespace PartyKing.Application.Games.Services.Ringo;

public interface IRingoRuleSuggestionService
{
    /// <summary>
    /// Host or admin: a few new game rules in <paramref name="language"/> for a flipped ace, none of them among <paramref name="previousRules"/>.
    /// Falls back to a built-in list when the language model is unavailable. Forbidden for anyone else, validation
    /// error for an oversized list of previous rules.
    /// </summary>
    Task<ErrorOr<GameSuggestionsDto>> SuggestAsync(
        Guid sessionId,
        IReadOnlyList<string>? previousRules,
        SessionCaller caller,
        UiLanguage language,
        CancellationToken cancellationToken);
}

internal class RingoRuleSuggestionService : IRingoRuleSuggestionService
{
    internal const int Count = 5;
    internal const int MaxLength = 200;
    internal const int MaxPreviousRules = 50;

    /// <summary>Everything the model and the fallback need in one language.</summary>
    private sealed record Texts(string SystemPrompt, string UserPrompt, string AvoidPrompt, IReadOnlyList<string> Fallback);

    // Shown when the model can't be reached, so the host always gets something to announce
    private static readonly Texts Polish = new(
        "Jesteś prowadzącym imprezę dla dorosłych (18+). Wymyślasz nowe zasady do karcianej gry imprezowej Ringo po polsku. " +
        "Zasada obowiązuje wszystkich do końca gry i zmienia sposób, w jaki się bawicie (np. zakaz słów, specjalne gesty, " +
        "podwójne kary, mówienie tylko pytaniami). Ma być zabawna, krótka (jedno zdanie) i możliwa do wykonania przy stole, " +
        "bez przemocy, przestępstw, nienawiści, osób nieletnich i drastycznych szczegółów. " +
        "Odpowiedz wyłącznie tablicą JSON z samymi napisami, bez żadnego dodatkowego tekstu.",
        $"Podaj {Count} różnych zasad. Seed: {{0}}.",
        " Nie powtarzaj tych zasad: ",
        [
            "Od teraz nie wolno wymieniać imion – kto się pomyli, pije łyka.",
            "Każdy toast musi zaczynać się od rymowanki.",
            "Nie wolno wskazywać palcem – kto wskaże, pije.",
            "Do każdego zdania trzeba dodać „proszę Wysoki Sądzie” – kto zapomni, pije.",
            "Pić wolno tylko lewą ręką, kto złamie zasadę, pije łyka za karę.",
            "Mówimy po sobie wyłącznie szeptem, kto podniesie głos, pije.",
            "Przed każdym łykiem trzeba powiedzieć komplement osobie po prawej.",
        ]);

    private static readonly Texts English = new(
        "You host a party for adults (18+). You invent new rules for the Ringo card party game in English. " +
        "A rule applies to everyone until the end of the game and changes how you play (e.g. banned words, special " +
        "gestures, double penalties, speaking only in questions). It should be funny, short (one sentence) and doable " +
        "at a table, without violence, crimes, hatred, minors or graphic details. " +
        "Reply only with a JSON array of strings, without any additional text.",
        $"Give {Count} different rules. Seed: {{0}}.",
        " Do not repeat these rules: ",
        [
            "From now on nobody may say anyone's name - whoever slips up takes a sip.",
            "Every toast has to start with a rhyme.",
            "Pointing with a finger is forbidden - whoever points drinks.",
            "Every sentence must end with \"your Honour\" - whoever forgets drinks.",
            "You may drink only with your left hand, and whoever breaks the rule takes a penalty sip.",
            "Everyone speaks in a whisper - whoever raises their voice drinks.",
            "Before every sip you must pay a compliment to the person on your right.",
        ]);

    private readonly ISessionAuthorizer _sessionAuthorizer;
    private readonly IChatClient _chatClient;
    private readonly LlmConfiguration _configuration;
    private readonly ILogger<RingoRuleSuggestionService> _logger;

    public RingoRuleSuggestionService(
        ISessionAuthorizer sessionAuthorizer,
        IChatClient chatClient,
        IOptions<LlmConfiguration> configuration,
        ILogger<RingoRuleSuggestionService> logger)
    {
        _sessionAuthorizer = sessionAuthorizer;
        _chatClient = chatClient;
        _configuration = configuration.Value;
        _logger = logger;
    }

    public async Task<ErrorOr<GameSuggestionsDto>> SuggestAsync(
        Guid sessionId,
        IReadOnlyList<string>? previousRules,
        SessionCaller caller,
        UiLanguage language,
        CancellationToken cancellationToken)
    {
        if (await _sessionAuthorizer.AuthorizeManagerAsync(sessionId, caller, cancellationToken)
            is { IsError: true } denied)
        {
            return denied.Errors;
        }

        // The list comes from the client and goes into the prompt, so it is bounded
        previousRules ??= [];
        if (previousRules.Count > MaxPreviousRules || previousRules.Any(rule => rule is null || rule.Length > MaxLength))
        {
            return Error.Validation(
                "Ringo.InvalidPreviousRules",
                $"At most {MaxPreviousRules} previous rules of up to {MaxLength} characters are accepted.");
        }

        var previous = previousRules.Select(rule => rule.Trim()).Where(rule => rule.Length > 0).Distinct().ToList();
        var isNew = (string rule) => !previous.Contains(rule, StringComparer.OrdinalIgnoreCase);

        var texts = language == UiLanguage.Pl ? Polish : English;
        var suggestions = SuggestionParser.Parse(await AskAsync(texts, previous, cancellationToken), Count, MaxLength)
            .Where(isNew)
            .ToList();
        if (suggestions.Count > 0)
        {
            return new GameSuggestionsDto(suggestions, FromFallback: false);
        }

        // Every built-in rule may already have been used; then the list is shown again rather than nothing
        var fresh = texts.Fallback.Where(isNew).ToList();

        return new GameSuggestionsDto(
            (fresh.Count > 0 ? fresh : texts.Fallback).OrderBy(_ => Random.Shared.Next()).Take(Count).ToList(),
            FromFallback: true);
    }

    /// <summary>The model's raw answer; null when there is no key or the model could not be reached (quota, timeout, outage).</summary>
    private async Task<string?> AskAsync(Texts texts, IReadOnlyList<string> previousRules, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_configuration.ApiKey))
        {
            return null;
        }

        var userPrompt = string.Format(texts.UserPrompt, Random.Shared.Next());
        if (previousRules.Count > 0)
        {
            userPrompt += texts.AvoidPrompt + string.Join(" | ", previousRules);
        }

        try
        {
            var response = await _chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, texts.SystemPrompt),
                    new ChatMessage(ChatRole.User, userPrompt),
                ],
                cancellationToken: cancellationToken);

            return response.Text;
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(e, "Could not get rules from the LLM");
            return null;
        }
    }
}
