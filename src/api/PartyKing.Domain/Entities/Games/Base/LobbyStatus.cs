namespace PartyKing.Domain.Entities.Games.Base;

public enum LobbyStatus
{
    NoGame,
    NotOpen,

    /// <summary>The game has started, so its players no longer change.</summary>
    Started,
    NameTaken,

    /// <summary>The game already has as many players as it allows.</summary>
    Full,
    NoPlayer,

    /// <summary>The ids to reorder are not exactly the game's players.</summary>
    OrderMismatch,
    Done,
}
