using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;

namespace PartyKing.Application.Tests;

/// <summary>Builds the session services with fakes, so a test lists only what it cares about.</summary>
internal static class SessionTestServices
{
    public static SessionAccessService Access(
        FakeSessionsRepository sessions,
        IPasswordHasher? hasher = null,
        SessionDummyHash? dummyHash = null)
    {
        hasher ??= new PasswordHasher();

        return new SessionAccessService(
            sessions,
            hasher,
            new SessionAuthorizer(sessions),
            dummyHash ?? new SessionDummyHash(hasher),
            NullLogger<SessionAccessService>.Instance);
    }

    public static SessionAdminService Admin(
        FakeSessionsRepository sessions,
        PlayerStateStore? playerStates = null,
        FakeHostTokenProvider? tokens = null,
        FakeSlideshowService? slideshows = null)
    {
        sessions.Events = TestEvents.SessionDispatcher(
            cleanup: new HostSessionCleanup(
                playerStates ?? new PlayerStateStore(new FakeClock()),
                tokens ?? new FakeHostTokenProvider(),
                new NullPlayerNotifier(),
                new NullPlayerConnectionWatchdog()),
            slideshow: slideshows ?? new FakeSlideshowService());

        return new SessionAdminService(
            sessions,
            new PasswordHasher(),
            NullLogger<SessionAdminService>.Instance);
    }
}
