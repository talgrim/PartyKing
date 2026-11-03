using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Events.Sessions;

namespace PartyKing.Application.Tests;

public class SessionTests
{
    private static Session NewSession() => new("ABC123", "Party", "hash");

    [Theory]
    [InlineData(null, SessionNameFailure.Empty)]
    [InlineData("", SessionNameFailure.Empty)]
    [InlineData("   ", SessionNameFailure.Empty)]
    [InlineData("Party", SessionNameFailure.None)]
    public void Names_AreValidatedAfterTrimming(string? name, SessionNameFailure expected)
    {
        Assert.Equal(expected, SessionNames.Validate(name));
    }

    [Fact]
    public void ANameOfExactlyTheMaximumLength_IsValid_EvenWithSpacesAround()
    {
        var name = $"  {new string('a', SessionNames.MaxLength)}  ";

        Assert.Equal(SessionNameFailure.None, SessionNames.Validate(name));
    }

    [Fact]
    public void ANameOneCharacterOverTheMaximum_IsTooLong()
    {
        Assert.Equal(SessionNameFailure.TooLong, SessionNames.Validate(new string('a', SessionNames.MaxLength + 1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void ABlankNameFallsBackToTheDefault(string? name)
    {
        Assert.Equal("New session", SessionNames.Normalize(name));
        Assert.Equal("New session", new Session("ABC123", name!, "hash").Name);
    }

    [Fact]
    public void ANewSession_RaisesNothing()
    {
        Assert.Empty(NewSession().DrainEvents());
    }

    [Fact]
    public void Rename_TrimsTheNameAndRaisesSessionRenamed()
    {
        var session = NewSession();

        session.Rename("  Friday  ");

        Assert.Equal("Friday", session.Name);
        Assert.Equal(new SessionRenamed(session.Id, "Friday"), Assert.Single(session.DrainEvents()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Rename_RefusesABlankName_AndRaisesNothing(string? name)
    {
        var session = NewSession();

        Assert.Throws<ArgumentException>(() => session.Rename(name));

        Assert.Equal("Party", session.Name);
        Assert.Empty(session.DrainEvents());
    }

    [Fact]
    public void ChangePassword_ReplacesTheHashAndRaisesAnEventWithoutIt()
    {
        var session = NewSession();

        session.ChangePassword("new-hash");

        Assert.Equal("new-hash", session.PasswordHash);
        Assert.Equal(new SessionPasswordChanged(session.Id), Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void SetFallbackPlaylist_RewindsItAndNamesThePreviousOne()
    {
        var session = NewSession();
        session.SetFallbackPlaylist("first", "First", shuffle: false);
        session.FallbackCursor = 4;
        session.DrainEvents();

        session.SetFallbackPlaylist("second", "Second", shuffle: true);

        Assert.Equal(("second", "Second", true, 0), (session.FallbackPlaylistId, session.FallbackPlaylistName, session.FallbackShuffle, session.FallbackCursor));
        Assert.Equal(new FallbackPlaylistChanged(session.Id, "first", "second"), Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void ClearFallbackPlaylist_RaisesOnlyWhenThereWasOne()
    {
        var session = NewSession();

        session.ClearFallbackPlaylist();
        Assert.Empty(session.DrainEvents());

        session.SetFallbackPlaylist("first", "First", shuffle: true);
        session.DrainEvents();
        session.ClearFallbackPlaylist();

        Assert.Null(session.FallbackPlaylistId);
        Assert.False(session.FallbackShuffle);
        Assert.Equal(new FallbackPlaylistCleared(session.Id, "first"), Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void Release_RemovesTheHostAndTheirPlaylist_NamingBoth()
    {
        var session = NewSession();
        session.HostProfileId = "host-1";
        session.SetFallbackPlaylist("first", "First", shuffle: true);
        session.DrainEvents();

        session.Release();

        Assert.Null(session.HostProfileId);
        Assert.Null(session.FallbackPlaylistId);
        Assert.Collection(
            session.DrainEvents(),
            first => Assert.Equal(new HostReleased(session.Id, "host-1"), first),
            second => Assert.Equal(new FallbackPlaylistCleared(session.Id, "first"), second));
    }

    [Fact]
    public void Release_WithoutAHost_StillRaisesHostReleased()
    {
        var session = NewSession();

        session.Release();

        Assert.Equal(new HostReleased(session.Id, null), Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void DrainEvents_ForgetsWhatItReturned()
    {
        var session = NewSession();
        session.Rename("Friday");

        Assert.Single(session.DrainEvents());
        Assert.Empty(session.DrainEvents());
    }
}
