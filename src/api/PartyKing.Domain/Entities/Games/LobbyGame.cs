namespace PartyKing.Domain.Entities.Games;

/// <summary>A game of one session (at most one per kind) that guests join by name once an admin opens it.</summary>
public abstract class LobbyGame<TPlayer>
    where TPlayer : LobbyPlayer
{
    public Guid SessionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set by an admin once guests may join; it cannot be undone.</summary>
    public bool IsOpen { get; set; }

    public List<TPlayer> Players { get; set; } = [];
}
