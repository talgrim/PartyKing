using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Demo.Services;
using PartyKing.Application.Ports;
using PartyKing.Domain.Entities.Demo;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Results;

namespace PartyKing.Application.Tests;

public class DemoSessionCleanupTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeSpotifyProfilesRepository _profiles = new();
    private readonly FakeDemoSessionsRepository _demoSessions = new();
    private readonly FakeSlideshowService _slideshows = new();
    private readonly FakeClock _clock = new();

    private DemoSessionCleanup CreateCleanup(ISpotifyProfilesRepository? profiles = null)
    {
        return new DemoSessionCleanup(
            _demoSessions,
            SessionTestServices.Admin(_sessions, slideshows: _slideshows),
            profiles ?? _profiles,
            _clock,
            NullLogger<DemoSessionCleanup>.Instance);
    }

    /// <summary>A demo session that ends <paramref name="endsIn"/> from now, with its synthetic host.</summary>
    private async Task<(Guid SessionId, string ProfileId)> AddDemoSessionAsync(TimeSpan endsIn)
    {
        var profileId = $"demo-{Guid.NewGuid():N}";
        var session = _sessions.AddSession(hostProfileId: profileId);
        await _profiles.CreateProfileAsync(new AuthenticationResult(profileId, "Demo host", "demo", $"refresh-{profileId}", 60), Ct);
        await _demoSessions.AddAsync(DemoSession.Start(session.Id, profileId, _clock.UtcNow.UtcDateTime, endsIn), Ct);

        return (session.Id, profileId);
    }

    [Fact]
    public async Task Sweep_DeletesTheExpiredSessions_WithTheirFilesAndTheirHostProfiles()
    {
        var expired = await AddDemoSessionAsync(TimeSpan.FromMinutes(-1));
        var alive = await AddDemoSessionAsync(TimeSpan.FromMinutes(60));

        var deleted = await CreateCleanup().SweepAsync(Ct);

        Assert.Equal(1, deleted);
        Assert.Null(await _sessions.GetAsync(expired.SessionId, Ct));
        Assert.Equal([expired.SessionId], _slideshows.DeletedSessions);
        Assert.Equal([expired.ProfileId], _profiles.Deleted);
        Assert.NotNull(await _sessions.GetAsync(alive.SessionId, Ct));
        Assert.Contains(alive.ProfileId, _profiles.Profiles.Keys);
    }

    [Fact]
    public async Task Sweep_KeepsASessionUntilTheInstantItExpires()
    {
        var session = await AddDemoSessionAsync(TimeSpan.FromMinutes(30));
        var cleanup = CreateCleanup();

        _clock.Advance(TimeSpan.FromMinutes(30).Subtract(TimeSpan.FromSeconds(1)));
        var before = await cleanup.SweepAsync(Ct);
        _clock.Advance(TimeSpan.FromSeconds(1));
        var at = await cleanup.SweepAsync(Ct);

        Assert.Equal(0, before);
        Assert.Equal(1, at);
        Assert.Null(await _sessions.GetAsync(session.SessionId, Ct));
    }

    [Fact]
    public async Task Sweep_WithNothingExpired_DeletesNothing()
    {
        await AddDemoSessionAsync(TimeSpan.FromMinutes(60));

        var deleted = await CreateCleanup().SweepAsync(Ct);

        Assert.Equal(0, deleted);
        Assert.Empty(_slideshows.DeletedSessions);
        Assert.Empty(_profiles.Deleted);
    }

    [Fact]
    public async Task Sweep_WhenOneSessionCannotBeCleaned_StillCleansTheOthers()
    {
        var stuck = await AddDemoSessionAsync(TimeSpan.FromMinutes(-2));
        var other = await AddDemoSessionAsync(TimeSpan.FromMinutes(-1));
        var profiles = new FailingProfiles(_profiles, failFor: stuck.ProfileId);

        var deleted = await CreateCleanup(profiles).SweepAsync(Ct);

        Assert.Equal(1, deleted);
        Assert.Equal([other.ProfileId], _profiles.Deleted);
        Assert.Null(await _sessions.GetAsync(other.SessionId, Ct));
    }

    private sealed class FailingProfiles(FakeSpotifyProfilesRepository inner, string failFor) : ISpotifyProfilesRepository
    {
        public Task CreateProfileAsync(AuthenticationResult result, CancellationToken cancellationToken) =>
            inner.CreateProfileAsync(result, cancellationToken);

        public Task<SpotifyProfile?> GetProfileAsync(string id, CancellationToken cancellationToken) =>
            inner.GetProfileAsync(id, cancellationToken);

        public Task DeleteProfileAsync(string id, CancellationToken cancellationToken) =>
            id == failFor ? throw new InvalidOperationException("the database is locked") : inner.DeleteProfileAsync(id, cancellationToken);
    }
}
