using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Events.Sessions;

namespace PartyKing.Domain.Tests;

public class SessionTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static Session NewSession()
    {
        return Session.Create(Guid.NewGuid(), "ABC123", "Party", "hash", Now).Session!;
    }

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
        Assert.Equal("New session", Session.Create(Guid.NewGuid(), "ABC123", name, "hash", Now).Session!.Name);
    }

    [Fact]
    public void Create_TakesTheIdAndTheTimeItIsGiven_AndTrimsTheName()
    {
        var id = Guid.NewGuid();

        var creation = Session.Create(id, "ABC123", "  Friday  ", "hash", Now);

        Assert.Equal(SessionNameFailure.None, creation.Failure);
        Assert.Equal((id, "ABC123", "Friday", "hash", Now), (creation.Session!.Id, creation.Session.Code, creation.Session.Name, creation.Session.PasswordHash, creation.Session.CreatedAt));
    }

    [Fact]
    public void Create_RefusesANameOverTheMaximum_AndMakesNoSession()
    {
        var creation = Session.Create(Guid.NewGuid(), "ABC123", new string('a', SessionNames.MaxLength + 1), "hash", Now);

        Assert.Equal(SessionNameFailure.TooLong, creation.Failure);
        Assert.Null(creation.Session);
    }

    [Fact]
    public void Create_AcceptsANameOfExactlyTheMaximum_EvenWithSpacesAround()
    {
        var name = $"  {new string('a', SessionNames.MaxLength)}  ";

        var creation = Session.Create(Guid.NewGuid(), "ABC123", name, "hash", Now);

        Assert.Equal(new string('a', SessionNames.MaxLength), creation.Session!.Name);
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

        var failure = session.Rename("  Friday  ");

        Assert.Equal(SessionNameFailure.None, failure);
        Assert.Equal("Friday", session.Name);
        Assert.Equal(new SessionRenamed(session.Id, "Friday"), Assert.Single(session.DrainEvents()));
    }

    [Theory]
    [InlineData(null, SessionNameFailure.Empty)]
    [InlineData(" ", SessionNameFailure.Empty)]
    [InlineData("over", SessionNameFailure.TooLong)]
    public void Rename_RefusesABadName_ChangesNothing_AndRaisesNothing(string? name, SessionNameFailure expected)
    {
        var session = NewSession();
        var badName = name == "over" ? new string('a', SessionNames.MaxLength + 1) : name;

        var failure = session.Rename(badName);

        Assert.Equal(expected, failure);
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
