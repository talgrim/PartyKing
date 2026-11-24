using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PartyKing.API.Authentication;
using PartyKing.API.Controllers;
using PartyKing.Application.Configuration;
using PartyKing.Application.System;
using ErrorOr;
using PartyKing.Application.Playlists.Services;
using PartyKing.Contract.V1.Playlists;
using PartyKing.Contract.V1.Enums;

namespace PartyKing.Api.Tests;

/// <summary>
/// The events the identity service delivers as signed webhooks (<c>POST /api/internal/events</c>, which nginx shortens
/// to <c>/internal/events</c>): signed with the shared secret over the timestamp and the exact bytes of the body, and
/// refused when the signature is wrong, the body was changed after signing or the timestamp is old.
/// </summary>
public class InternalEventsControllerTests
{
    private const string Secret = "shared-secret";
    private static readonly Guid AccountId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new() { UtcNow = Now };
    private readonly IdentityConfiguration _settings = new() { WebhookSecret = Secret };
    private readonly FakeSavedPlaylists _playlists = new();

    private InternalEventsController Controller(DefaultHttpContext http)
    {
        var verifier = new IdentityWebhookVerifier(Options.Create(_settings), _clock);

        return new InternalEventsController(new HttpContextAccessor(), verifier, _playlists)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private static string DeletedBody(Guid id, string eventId = "5f0c2f0e-6a0b-4e83-b9a5-0d1d3c2f7a11") =>
        $$$"""{"id":"{{{eventId}}}","type":"account.deleted","occurredAt":"2026-11-24T12:00:00Z","data":{"accountId":"{{{id}}}"}}""";

    /// <summary>A call as the identity service makes it; what is passed in is what goes wrong with it.</summary>
    private static DefaultHttpContext Call(
        string body,
        string? secret = Secret,
        DateTimeOffset? at = null,
        string? signedBody = null,
        string? signature = null)
    {
        var timestamp = (at ?? Now).ToUnixTimeSeconds().ToString();
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Path = "/internal/events";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        http.Request.Headers[IdentityWebhookVerifier.TimestampHeader] = timestamp;
        http.Request.Headers[IdentityWebhookVerifier.SignatureHeader] = signature
            ?? IdentityWebhookVerifier.Sign(secret!, timestamp, Encoding.UTF8.GetBytes(signedBody ?? body));

        return http;
    }

    private Task<IActionResult> ReceiveAsync(DefaultHttpContext http) =>
        Controller(http).Receive(CancellationToken.None);

    [Fact]
    public async Task ASignedAccountDeleted_DeletesThePlaylistsOfThatAccount_AndAnswers204()
    {
        var result = await ReceiveAsync(Call(DeletedBody(AccountId)));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal([AccountId], _playlists.Deleted);
    }

    [Fact]
    public async Task ARetriedEvent_IsAnswered204Again()
    {
        await ReceiveAsync(Call(DeletedBody(AccountId)));

        Assert.IsType<NoContentResult>(await ReceiveAsync(Call(DeletedBody(AccountId))));
    }

    [Fact]
    public async Task ASignedEventOfATypeItDoesNotKnow_IsIgnored_ButAnswered204()
    {
        var result = await ReceiveAsync(Call("""{"id":"x","type":"password.changed","data":{}}"""));

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(_playlists.Deleted);
    }

    [Fact]
    public async Task ACallSignedWithAnotherSecret_IsRefused_AndDeletesNothing()
    {
        var result = await ReceiveAsync(Call(DeletedBody(AccountId), secret: "someone-elses"));

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(_playlists.Deleted);
    }

    [Fact]
    public async Task ABodyChangedAfterSigning_IsRefused()
    {
        var result = await ReceiveAsync(Call(DeletedBody(Guid.NewGuid()), signedBody: DeletedBody(AccountId)));

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(_playlists.Deleted);
    }

    [Fact]
    public async Task TheSignature_DoesNotDependOnThePath()
    {
        var http = Call(DeletedBody(AccountId));
        http.Request.Path = "/api/internal/events";

        Assert.IsType<NoContentResult>(await ReceiveAsync(http));
    }

    [Fact]
    public async Task ACallWithoutASignature_IsRefused()
    {
        var http = Call(DeletedBody(AccountId));
        http.Request.Headers.Remove(IdentityWebhookVerifier.SignatureHeader);

        Assert.IsType<UnauthorizedResult>(await ReceiveAsync(http));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-number")]
    [InlineData("-5")]
    public async Task ACallWithAnUnreadableTimestamp_IsRefused(string timestamp)
    {
        var http = Call(DeletedBody(AccountId));
        http.Request.Headers[IdentityWebhookVerifier.TimestampHeader] = timestamp;

        Assert.IsType<UnauthorizedResult>(await ReceiveAsync(http));
    }

    [Fact]
    public async Task ACallFromSixMinutesAgo_IsRefused_ButFourMinutesIsNot()
    {
        var old = await ReceiveAsync(Call(DeletedBody(AccountId), at: Now.AddMinutes(-6)));
        var recent = await ReceiveAsync(Call(DeletedBody(AccountId), at: Now.AddMinutes(-4)));

        Assert.IsType<UnauthorizedResult>(old);
        Assert.IsType<NoContentResult>(recent);
    }

    [Fact]
    public async Task ACallFromTheFuture_IsRefused()
    {
        Assert.IsType<UnauthorizedResult>(await ReceiveAsync(Call(DeletedBody(AccountId), at: Now.AddMinutes(6))));
    }

    [Fact]
    public async Task WithoutAConfiguredSecret_EveryCallIsRefused_EvenOneSignedWithAnEmptyKey()
    {
        _settings.WebhookSecret = null;

        Assert.IsType<UnauthorizedResult>(await ReceiveAsync(Call(DeletedBody(AccountId), secret: "")));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"id":"x"}""")]
    [InlineData("""{"type":"account.deleted","data":{}}""")]
    [InlineData("""{"type":"account.deleted","data":{"accountId":"nope"}}""")]
    [InlineData("""{"type":"account.deleted","data":{"accountId":"00000000-0000-0000-0000-000000000000"}}""")]
    public async Task ASignedBodyThatIsNotAnEvent_Is400_AndDeletesNothing(string body)
    {
        var result = await ReceiveAsync(Call(body));

        Assert.IsType<BadRequestResult>(result);
        Assert.Empty(_playlists.Deleted);
    }

    [Fact]
    public async Task ABodyOverTheLimit_IsRefused()
    {
        var body = DeletedBody(AccountId) + new string(' ', InternalEventsController.MaxBodyBytes);

        Assert.IsType<UnauthorizedResult>(await ReceiveAsync(Call(body)));
        Assert.Empty(_playlists.Deleted);
    }

    [Fact]
    public void TheEndpoint_IsRateLimited_SizeLimited_AndOutOfSwagger()
    {
        var method = typeof(InternalEventsController).GetMethod(nameof(InternalEventsController.Receive))!;

        Assert.NotNull(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), false).SingleOrDefault());
        Assert.NotNull(method.GetCustomAttributes(typeof(RequestSizeLimitAttribute), false).SingleOrDefault());
        Assert.True(((ApiExplorerSettingsAttribute)Attribute.GetCustomAttribute(
            typeof(InternalEventsController),
            typeof(ApiExplorerSettingsAttribute))!).IgnoreApi);
    }

    private sealed class FakeClock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class FakeSavedPlaylists : ISavedPlaylistService
    {
        public List<Guid> Deleted { get; } = [];

        public Task<ErrorOr<Success>> DeleteByOwnerAsync(Guid accountId, CancellationToken cancellationToken)
        {
            Deleted.Add(accountId);

            return Task.FromResult<ErrorOr<Success>>(Result.Success);
        }

        public Task<ErrorOr<SavedPlaylistDto>> SaveQueueAsync(
            Guid accountId,
            Guid sessionId,
            string? name,
            IReadOnlyCollection<PlaylistSection> sections,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ErrorOr<IReadOnlyList<SavedPlaylistDto>>> ListAsync(Guid accountId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ErrorOr<SavedPlaylistPreviewDto>> GetPreviewAsync(Guid sessionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
