using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Games.Services.Ringo;
using PartyKing.Application.Localization;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Games;
using PartyKing.Infrastructure.Llm;

namespace PartyKing.Application.Tests;

public class RingoRuleSuggestionServiceTests
{
    private const string HostId = "host-1";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeChatClient _chat = new();
    private readonly LlmConfiguration _configuration = new() { ApiKey = "key" };
    private readonly RingoRuleSuggestionService _service;
    private readonly Guid _id;

    public RingoRuleSuggestionServiceTests()
    {
        var clock = new FakeClock();
        var sessionService = new SessionService(
            _sessions,
            new PasswordHasher(),
            new PlayerStateStore(clock),
            new FakeHostTokenProvider(),
            new FakeSlideshowService(),
            JoinTokens.Create(clock),
            clock,
            new NullSessionMusicNotifier(),
            new NullPlayerConnectionWatchdog(),
            NullLogger<SessionService>.Instance);
        _service = new RingoRuleSuggestionService(
            sessionService,
            _chat,
            Options.Create(_configuration),
            NullLogger<RingoRuleSuggestionService>.Instance);
        _id = _sessions.AddSession(HostId, new PasswordHasher().Hash("secret")).Id;
    }

    private Task<ErrorOr.ErrorOr<GameSuggestionsDto>> SuggestAsync(
        IReadOnlyList<string>? previous = null,
        UiLanguage language = UiLanguage.Pl) =>
        _service.SuggestAsync(_id, previous, HostId, false, language, CancellationToken.None);

    [Fact]
    public async Task Guests_AreForbidden_AndTheModelIsNotAsked()
    {
        var result = await _service.SuggestAsync(_id, null, "someone-else", false, UiLanguage.Pl, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Forbidden, result.FirstError.Type);
        Assert.Equal(0, _chat.Calls);
    }

    [Fact]
    public async Task TheModelsRules_AreReturned()
    {
        _chat.Reply = """["Reguła A.", "Reguła B."]""";

        var result = await SuggestAsync();

        Assert.False(result.Value.FromFallback);
        Assert.Equal(["Reguła A.", "Reguła B."], result.Value.Suggestions);
    }

    [Fact]
    public async Task PreviousRules_AreSentToTheModel_AndDroppedFromItsAnswer()
    {
        _chat.Reply = """["Reguła A.", "reguła b.", "Reguła C."]""";

        var result = await SuggestAsync(["Reguła A.", "Reguła B."]);

        Assert.Equal(["Reguła C."], result.Value.Suggestions);
        Assert.Contains("Nie powtarzaj tych zasad: Reguła A. | Reguła B.", _chat.LastUserPrompt);
    }

    [Fact]
    public async Task WithoutPreviousRules_ThePromptHasNoExclusions()
    {
        _chat.Reply = """["Reguła A."]""";

        await SuggestAsync();

        Assert.DoesNotContain("Nie powtarzaj", _chat.LastUserPrompt);
    }

    [Fact]
    public async Task InEnglish_TheModelIsAskedInEnglish_AndToAvoidThePreviousRules()
    {
        _chat.Reply = """["Rule A.", "Rule B."]""";

        var result = await SuggestAsync(["Rule A."], UiLanguage.En);

        Assert.Equal(["Rule B."], result.Value.Suggestions);
        Assert.Contains("in English", _chat.LastSystemPrompt);
        Assert.Contains("Do not repeat these rules: Rule A.", _chat.LastUserPrompt);
        Assert.DoesNotContain("Nie powtarzaj", _chat.LastUserPrompt);
    }

    [Fact]
    public async Task InPolish_TheModelIsAskedInPolish()
    {
        _chat.Reply = """["Reguła A."]""";

        await SuggestAsync(language: UiLanguage.Pl);

        Assert.Contains("po polsku", _chat.LastSystemPrompt);
        Assert.Contains("Podaj", _chat.LastUserPrompt);
    }

    [Fact]
    public async Task TheEnglishBuiltInList_HasNoPolishRules_AndSkipsThePreviousOnes()
    {
        _configuration.ApiKey = null;

        var first = (await SuggestAsync(language: UiLanguage.En)).Value.Suggestions;
        var second = (await SuggestAsync(first, UiLanguage.En)).Value.Suggestions;

        Assert.All(first.Concat(second), rule => Assert.DoesNotContain(rule, c => "ąćęłńóśźż„".Contains(c)));
        Assert.Empty(first.Intersect(second));
    }

    [Fact]
    public async Task WithoutAnApiKey_TheBuiltInListIsUsed_WithoutCallingTheModel()
    {
        _configuration.ApiKey = null;

        var result = await SuggestAsync();

        Assert.True(result.Value.FromFallback);
        Assert.NotEmpty(result.Value.Suggestions);
        Assert.Equal(0, _chat.Calls);
    }

    [Fact]
    public async Task TheBuiltInList_SkipsPreviousRules()
    {
        _configuration.ApiKey = null;
        var first = (await SuggestAsync()).Value.Suggestions;

        var second = (await SuggestAsync(first)).Value.Suggestions;

        Assert.Empty(second.Intersect(first));
    }

    [Fact]
    public async Task WhenTheWholeBuiltInListWasUsed_ItIsShownAgain()
    {
        _configuration.ApiKey = null;
        var all = new List<string>();
        for (var i = 0; i < 20; i++)
        {
            all.AddRange((await SuggestAsync(all)).Value.Suggestions.Except(all));
        }

        var result = await SuggestAsync(all);

        Assert.True(result.Value.FromFallback);
        Assert.NotEmpty(result.Value.Suggestions);
    }

    [Fact]
    public async Task WhenTheModelFails_TheBuiltInListIsUsed()
    {
        _chat.Failure = new LlmProviderException(503, "overloaded", new Exception());

        var result = await SuggestAsync();

        Assert.True(result.Value.FromFallback);
        Assert.NotEmpty(result.Value.Suggestions);
    }

    [Fact]
    public async Task WhenTheAnswerCannotBeParsed_TheBuiltInListIsUsed()
    {
        _chat.Reply = "Sorry, no.";

        var result = await SuggestAsync();

        Assert.True(result.Value.FromFallback);
    }

    [Fact]
    public async Task ATooLongListOfPreviousRules_IsRejected_WithoutCallingTheModel()
    {
        var previous = Enumerable.Range(0, RingoRuleSuggestionService.MaxPreviousRules + 1).Select(i => $"r{i}").ToList();

        var result = await SuggestAsync(previous);

        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
        Assert.Equal(0, _chat.Calls);
    }

    [Fact]
    public async Task APreviousRuleThatIsTooLong_IsRejected()
    {
        var result = await SuggestAsync([new string('a', RingoRuleSuggestionService.MaxLength + 1)]);

        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task ACancelledRequest_IsNotSwallowed()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _chat.Failure = new OperationCanceledException(cts.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.SuggestAsync(_id, null, HostId, false, UiLanguage.Pl, cts.Token));
    }
}
