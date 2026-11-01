using ErrorOr;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Games.Services.Base;

/// <summary>
/// The errors of a game lobby, with the game's name as the prefix of their codes ("Ringo.NotOpen"). The codes and
/// messages are matched by the UI and the tests, so keep them stable.
/// </summary>
internal class LobbyErrors
{
    private readonly GameLobbyOptions _options;

    public LobbyErrors(GameLobbyOptions options)
    {
        _options = options;
    }

    public Error NoGame
    {
        get
        {
            return Error.NotFound($"{_options.Name}.NotFound", $"The session has no {_options.Name} game.");
        }
    }

    public Error SessionNotFound
    {
        get
        {
            return Error.NotFound($"{_options.Name}.SessionNotFound", "The session was not found.");
        }
    }

    public Error AdminOnly
    {
        get
        {
            return Error.Forbidden($"{_options.Name}.AdminOnly", "Only an admin can open the game to guests.");
        }
    }

    public Error InvalidOrder
    {
        get
        {
            return Error.Validation($"{_options.Name}.InvalidOrder", "The player ids are required.");
        }
    }

    /// <summary>The error of a lobby action's status, or null when the action was done.</summary>
    public Error? From(LobbyStatus status)
    {
        return status switch
        {
            LobbyStatus.Done => null,
            LobbyStatus.NoGame => NoGame,
            LobbyStatus.NotOpen => Error.Conflict($"{_options.Name}.NotOpen", "The game is not open to guests."),
            LobbyStatus.Started => Error.Conflict(
                $"{_options.Name}.Started", "The game has started, its players can no longer change."),
            LobbyStatus.NameTaken => Error.Conflict(
                $"{_options.Name}.NameTaken", "Another player already uses this name."),
            LobbyStatus.Full => Error.Conflict(
                $"{_options.Name}.Full", $"The game already has {_options.MaxPlayers} players."),
            LobbyStatus.NoPlayer => Error.NotFound(
                $"{_options.Name}.PlayerNotFound", "The game has no such player."),
            LobbyStatus.OrderMismatch => Error.Conflict(
                $"{_options.Name}.OrderMismatch", "The players changed, try again."),
            var unknown => throw new ArgumentOutOfRangeException(
                nameof(status), unknown, "No error is defined for this status."),
        };
    }

    public Error From(LobbyNameFailure failure)
    {
        return failure switch
        {
            LobbyNameFailure.Empty =>
                Error.Validation($"{_options.Name}.InvalidName", "The name must not be empty."),
            LobbyNameFailure.TooLong => Error.Validation(
                $"{_options.Name}.InvalidName", $"The name must be at most {LobbyNames.MaxLength} characters."),
            LobbyNameFailure.HostSuffix =>
                Error.Validation($"{_options.Name}.InvalidName", "The name must not end with (host)."),
            var unknown => throw new ArgumentOutOfRangeException(
                nameof(failure), unknown, "No error is defined for this failure."),
        };
    }
}
