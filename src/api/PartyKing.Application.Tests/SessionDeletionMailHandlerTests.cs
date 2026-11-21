using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Sessions.Handlers;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Events.Sessions;
using PartyKing.Domain.Repositories;

namespace PartyKing.Application.Tests;

public class SessionDeletionMailHandlerTests
{
    private const string Recipient = "partyking@talgrim.dev";
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTime Scheduled = new(2026, 11, 28, 14, 5, 0, DateTimeKind.Utc);

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeMailClient _mail = new();

    private SessionDeletionMailHandler CreateHandler(string? recipient = Recipient)
    {
        return new SessionDeletionMailHandler(
            _sessions,
            _mail,
            Options.Create(new MailConfiguration { Recipient = recipient }),
            NullLogger<SessionDeletionMailHandler>.Instance);
    }

    [Fact]
    public async Task TheMail_GoesToTheRecipient_AndNamesTheSessionItsCodeItsIdAndTheTime()
    {
        var session = _sessions.AddSession(passwordHash: "secret-hash");

        await CreateHandler().HandleAsync(new SessionDeletionScheduled(session.Id, Scheduled), Ct);

        var mail = Assert.Single(_mail.Sent);
        Assert.Equal(Recipient, mail.To);
        Assert.Contains("Party", mail.Subject);
        Assert.Contains("ABC123", mail.Subject);
        Assert.Contains(session.Id.ToString(), mail.Body);
        Assert.Contains("2026-11-28 14:05 UTC", mail.Body);
    }

    [Fact]
    public async Task TheMail_CarriesNoPasswordHashOrAccessStamp()
    {
        var session = _sessions.AddSession(passwordHash: "secret-hash");

        await CreateHandler().HandleAsync(new SessionDeletionScheduled(session.Id, Scheduled), Ct);

        var mail = Assert.Single(_mail.Sent);
        Assert.DoesNotContain("secret-hash", mail.Subject + mail.Body);
        Assert.DoesNotContain(session.AccessStamp, mail.Subject + mail.Body);
    }

    [Fact]
    public async Task ALineBreakInTheSessionName_DoesNotBreakTheSubject()
    {
        var session = new Session(Guid.NewGuid(), "ABC123", "Party\r\nBcc: x@example.com", "hash", DateTime.UtcNow);
        await _sessions.TryAddAsync(session, Ct);

        await CreateHandler().HandleAsync(new SessionDeletionScheduled(session.Id, Scheduled), Ct);

        Assert.DoesNotContain('\n', Assert.Single(_mail.Sent).Subject);
        Assert.DoesNotContain('\r', Assert.Single(_mail.Sent).Subject);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task WithoutARecipient_NothingIsSent_AndNothingThrows(string? recipient)
    {
        var session = _sessions.AddSession();

        await CreateHandler(recipient).HandleAsync(new SessionDeletionScheduled(session.Id, Scheduled), Ct);

        Assert.Empty(_mail.Sent);
    }

    [Fact]
    public async Task ASessionThatIsAlreadyGone_GetsNoMail()
    {
        await CreateHandler().HandleAsync(new SessionDeletionScheduled(Guid.NewGuid(), Scheduled), Ct);

        Assert.Empty(_mail.Sent);
    }

    [Fact]
    public async Task ARequest_SendsOneMail_AndAskingAgainSendsNoSecondOne()
    {
        _sessions.Events = TestEvents.SessionDeletionDispatcher(
            _sessions,
            _mail,
            new MailConfiguration { Recipient = Recipient });
        var session = _sessions.AddSession();

        await _sessions.ScheduleDeletionAsync(session.Id, Scheduled, TimeSpan.FromDays(7), Ct);
        await _sessions.ScheduleDeletionAsync(session.Id, Scheduled.AddDays(1), TimeSpan.FromDays(7), Ct);

        var mail = Assert.Single(_mail.Sent);
        Assert.Contains("2026-12-05 14:05 UTC", mail.Body);
    }

    [Fact]
    public async Task WhenTheMailFails_TheDeletionIsStillScheduled_AndTheRequestStillSucceeds()
    {
        _mail.Failure = new HttpRequestException("the mail provider is down");
        _sessions.Events = TestEvents.SessionDeletionDispatcher(
            _sessions,
            _mail,
            new MailConfiguration { Recipient = Recipient });
        var session = _sessions.AddSession();

        var result = await _sessions.ScheduleDeletionAsync(session.Id, Scheduled, TimeSpan.FromDays(7), Ct);

        Assert.Equal(1, _mail.Attempts);
        Assert.Equal(PartyKing.Domain.Enums.SessionDeletionScheduleResult.Scheduled, result);
        Assert.Equal(Scheduled.AddDays(7), (await _sessions.GetAsync(session.Id, Ct))!.DeletionScheduledAt);
    }
}
