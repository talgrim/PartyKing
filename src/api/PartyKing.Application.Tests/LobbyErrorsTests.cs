using ErrorOr;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Application.Tests;

/// <summary>
/// The golden table of the lobby errors by status and name failure: type, code and message, which the UI and the
/// tests match on. The game's name prefixes the codes.
/// </summary>
public class LobbyErrorsTests
{
    private record Expected(ErrorType Type, string Code, string Description);

    private static readonly LobbyErrors Ringo = new(new GameLobbyOptions("Ringo"));
    private static readonly LobbyErrors Resistance = new(new GameLobbyOptions("Resistance", 10));

    private static readonly Dictionary<LobbyStatus, Expected?> Statuses = new()
    {
        [LobbyStatus.Done] = null,
        [LobbyStatus.NoGame] = new(ErrorType.NotFound, "Resistance.NotFound", "The session has no Resistance game."),
        [LobbyStatus.NotOpen] = new(ErrorType.Conflict, "Resistance.NotOpen", "The game is not open to guests."),
        [LobbyStatus.Started] = new(
            ErrorType.Conflict, "Resistance.Started", "The game has started, its players can no longer change."),
        [LobbyStatus.NameTaken] = new(
            ErrorType.Conflict, "Resistance.NameTaken", "Another player already uses this name."),
        [LobbyStatus.Full] = new(ErrorType.Conflict, "Resistance.Full", "The game already has 10 players."),
        [LobbyStatus.NoPlayer] = new(
            ErrorType.NotFound, "Resistance.PlayerNotFound", "The game has no such player."),
        [LobbyStatus.OrderMismatch] = new(
            ErrorType.Conflict, "Resistance.OrderMismatch", "The players changed, try again."),
    };

    private static readonly Dictionary<LobbyNameFailure, Expected> NameFailures = new()
    {
        [LobbyNameFailure.Empty] = new(ErrorType.Validation, "Resistance.InvalidName", "The name must not be empty."),
        [LobbyNameFailure.TooLong] = new(
            ErrorType.Validation, "Resistance.InvalidName", "The name must be at most 30 characters."),
        [LobbyNameFailure.HostSuffix] = new(
            ErrorType.Validation, "Resistance.InvalidName", "The name must not end with (host)."),
    };

    [Fact]
    public void EveryStatusAndNameFailure_IsInTheTables()
    {
        Assert.Equal(Enum.GetValues<LobbyStatus>().Order(), Statuses.Keys.Order());
        Assert.Equal(Enum.GetValues<LobbyNameFailure>().Order(), NameFailures.Keys.Order());
    }

    [Fact]
    public void EachStatus_BecomesItsError_AndDoneBecomesNone()
    {
        foreach (var (status, expected) in Statuses)
        {
            var error = Resistance.From(status);

            if (expected is null)
            {
                Assert.Null(error);
                continue;
            }

            Assert.True(error.HasValue, $"{status} should be an error");
            Assert.Equal((expected.Type, expected.Code, expected.Description),
                (error.Value.Type, error.Value.Code, error.Value.Description));
        }
    }

    [Fact]
    public void EachNameFailure_BecomesItsError()
    {
        foreach (var (failure, expected) in NameFailures)
        {
            var error = Resistance.From(failure);

            Assert.Equal((expected.Type, expected.Code, expected.Description),
                (error.Type, error.Code, error.Description));
        }
    }

    [Fact]
    public void TheGameName_PrefixesTheCodesAndNamesTheGame()
    {
        Assert.Equal("Ringo.NotFound", Ringo.NoGame.Code);
        Assert.Equal("The session has no Ringo game.", Ringo.NoGame.Description);
        Assert.Equal("Ringo.NotOpen", Ringo.From(LobbyStatus.NotOpen)!.Value.Code);
    }

    [Fact]
    public void TheGameErrors_AreTheLobbysOwn()
    {
        Assert.Equal(
            (ErrorType.NotFound, "Resistance.SessionNotFound", "The session was not found."),
            (Resistance.SessionNotFound.Type, Resistance.SessionNotFound.Code, Resistance.SessionNotFound.Description));
        Assert.Equal(
            (ErrorType.Forbidden, "Resistance.AdminOnly", "Only an admin can open the game to guests."),
            (Resistance.AdminOnly.Type, Resistance.AdminOnly.Code, Resistance.AdminOnly.Description));
        Assert.Equal(
            (ErrorType.Validation, "Resistance.InvalidOrder", "The player ids are required."),
            (Resistance.InvalidOrder.Type, Resistance.InvalidOrder.Code, Resistance.InvalidOrder.Description));
    }
}
