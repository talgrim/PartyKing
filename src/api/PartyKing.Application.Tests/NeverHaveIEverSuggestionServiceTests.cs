using PartyKing.Application.Configuration;
using PartyKing.Application.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Games.Services.NeverHaveIEver;
using PartyKing.Application.Localization;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Sessions;
using PartyKing.Infrastructure.Llm;

namespace PartyKing.Application.Tests;

public class NeverHaveIEverSuggestionServiceTests
{
    private const string HostId = "host-1";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeChatClient _chat = new();
    private readonly LlmConfiguration _configuration = new() { ApiKey = "key" };
    private readonly NeverHaveIEverSuggestionService _service;
    private readonly Guid _id;

    public NeverHaveIEverSuggestionServiceTests()
    {
        var clock = new FakeClock();
        var sessionAuthorizer = new SessionAuthorizer(_sessions);
        _service = new NeverHaveIEverSuggestionService(
            sessionAuthorizer,
            _chat,
            Options.Create(_configuration),
            NullLogger<NeverHaveIEverSuggestionService>.Instance);
        _id = _sessions.AddSession(HostId, new PasswordHasher().Hash("secret")).Id;
    }

    private Task<ErrorOr.ErrorOr<PartyKing.Contract.V1.Games.GameSuggestionsDto>> SuggestAsync(
        UiLanguage language = UiLanguage.Pl) =>
        _service.SuggestAsync(_id, new SessionCaller(HostId, false), language, CancellationToken.None);

    [Fact]
    public async Task Guests_AreForbidden_AndTheModelIsNotAsked()
    {
        var result = await _service.SuggestAsync(_id, new SessionCaller("someone-else", false), UiLanguage.Pl, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Forbidden, result.FirstError.Type);
        Assert.Equal(0, _chat.Calls);
    }

    [Fact]
    public async Task TheModelsStatements_AreReturned()
    {
        _chat.Reply = """["Nigdy w życiu nie A.", "Nigdy w życiu nie B."]""";

        var result = await SuggestAsync();

        Assert.False(result.Value.FromFallback);
        Assert.Equal(["Nigdy w życiu nie A.", "Nigdy w życiu nie B."], result.Value.Suggestions);
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
    public async Task WhenTheModelFails_TheBuiltInListIsUsed()
    {
        _chat.Failure = new LlmProviderException(503, "overloaded", new Exception());

        var result = await SuggestAsync();

        Assert.True(result.Value.FromFallback);
        Assert.Equal(NeverHaveIEverSuggestionService.Count, result.Value.Suggestions.Count);
    }

    [Fact]
    public async Task InEnglish_TheModelIsAskedInEnglish()
    {
        _chat.Reply = """["Never have I ever A."]""";

        var result = await SuggestAsync(UiLanguage.En);

        Assert.Equal(["Never have I ever A."], result.Value.Suggestions);
        Assert.Contains("in English", _chat.LastSystemPrompt);
        Assert.Contains("Give", _chat.LastUserPrompt);
    }

    [Fact]
    public async Task InPolish_TheModelIsAskedInPolish()
    {
        _chat.Reply = """["Nigdy w życiu nie A."]""";

        await SuggestAsync(UiLanguage.Pl);

        Assert.Contains("po polsku", _chat.LastSystemPrompt);
        Assert.Contains("Podaj", _chat.LastUserPrompt);
    }

    [Theory]
    [InlineData(UiLanguage.En, "Never have I ever")]
    [InlineData(UiLanguage.Pl, "Nigdy w życiu nie")]
    public async Task TheBuiltInList_IsInTheLanguage(UiLanguage language, string start)
    {
        _configuration.ApiKey = null;

        var result = await SuggestAsync(language);

        Assert.True(result.Value.FromFallback);
        Assert.All(result.Value.Suggestions, statement => Assert.StartsWith(start, statement));
    }

    [Fact]
    public async Task AnAnswerThatIsNotAJsonArray_FallsBackToTheBuiltInList()
    {
        _chat.Reply = "Przepraszam, nie mogę.";

        Assert.True((await SuggestAsync()).Value.FromFallback);
    }

    [Fact]
    public async Task ACancelledRequest_IsNotSwallowed()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _chat.Failure = new OperationCanceledException(cancellation.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.SuggestAsync(_id, new SessionCaller(HostId, false), UiLanguage.Pl, cancellation.Token));
    }

    [Fact]
    public void Parse_ReadsAnArrayInsideAMarkdownFence()
    {
        var text = "```json\n[\"a\", \"b\"]\n```";

        Assert.Equal(["a", "b"], NeverHaveIEverSuggestionService.Parse(text));
    }

    [Fact]
    public void Parse_DropsBlankLongAndDuplicateItems_AndCapsTheCount()
    {
        var tooLong = new string('x', NeverHaveIEverSuggestionService.MaxLength + 1);
        var items = new[] { "  ", tooLong, "a", "a", "b", "c", "d", "e", "f" };
        var text = global::System.Text.Json.JsonSerializer.Serialize(items);

        Assert.Equal(["a", "b", "c", "d", "e"], NeverHaveIEverSuggestionService.Parse(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no array here")]
    [InlineData("[1, 2")]
    [InlineData("{\"a\": 1}")]
    public void Parse_ReturnsNothingForAnythingElse(string? text)
    {
        Assert.Empty(NeverHaveIEverSuggestionService.Parse(text));
    }
}
