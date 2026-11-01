namespace PartyKing.Domain.Entities.Games.Base;

/// <summary>
/// A game of one session (at most one per kind) that guests join by name once an admin opens it. It owns the
/// rules of its lobby: opening, joining, kicking and ordering the players.
/// </summary>
public abstract class LobbyGame<TPlayer>
    where TPlayer : LobbyPlayer
{
    public Guid SessionId { get; internal set; }

    public DateTimeOffset CreatedAt { get; internal set; }

    /// <summary>Set by an admin once guests may join; it cannot be undone.</summary>
    public bool IsOpen { get; internal set; }

    public List<TPlayer> Players { get; internal set; } = [];

    /// <summary>How many players may join.</summary>
    protected virtual int PlayerLimit => int.MaxValue;

    /// <summary>Whether the players are locked, so nobody joins, leaves or moves.</summary>
    protected virtual bool IsLocked => false;

    /// <summary>Called after the player was added to the game, so a game can add its own state.</summary>
    protected virtual void OnPlayerJoined(TPlayer player)
    {
    }

    /// <summary>Called while the player is still in the game, so a game can work out what follows their leaving.</summary>
    protected virtual void OnPlayerKicking(TPlayer kicked)
    {
    }

    /// <summary>Opens the game to guests. False when it was open already, so nothing changed.</summary>
    public bool Open()
    {
        if (IsOpen)
        {
            return false;
        }

        IsOpen = true;

        return true;
    }

    /// <summary>
    /// Adds the player at the end of the player order and gives them the normalized name that has to be unique in
    /// the game. Anything but <see cref="LobbyStatus.Done"/> leaves the game and the player as they were.
    /// </summary>
    public LobbyStatus Join(TPlayer player)
    {
        if (!IsOpen)
        {
            return LobbyStatus.NotOpen;
        }

        if (IsLocked)
        {
            return LobbyStatus.Started;
        }

        var normalizedName = LobbyNames.Normalize(player.DisplayName);
        if (Players.Any(x => x.NormalizedName == normalizedName))
        {
            return LobbyStatus.NameTaken;
        }

        if (Players.Count >= PlayerLimit)
        {
            return LobbyStatus.Full;
        }

        player.SessionId = SessionId;
        player.Order = Players.Count == 0 ? 0 : Players.Max(x => x.Order) + 1;
        Players.Add(player);
        OnPlayerJoined(player);

        return LobbyStatus.Done;
    }

    /// <summary>Removes a player and closes the gap in the order. The removed player is null unless the status is Done.</summary>
    public (LobbyStatus Status, TPlayer? Kicked) Kick(Guid playerId)
    {
        if (IsLocked)
        {
            return (LobbyStatus.Started, null);
        }

        var kicked = Players.FirstOrDefault(x => x.PlayerId == playerId);
        if (kicked is null)
        {
            return (LobbyStatus.NoPlayer, null);
        }

        OnPlayerKicking(kicked);

        Players.Remove(kicked);
        var order = 0;
        foreach (var player in Players.OrderBy(x => x.Order))
        {
            player.Order = order++;
        }

        return (LobbyStatus.Done, kicked);
    }

    /// <summary>Sets the player order to the given ids, which must be exactly the game's players.</summary>
    public LobbyStatus Reorder(IReadOnlyList<Guid> playerIds)
    {
        if (IsLocked)
        {
            return LobbyStatus.Started;
        }

        if (playerIds.Count != Players.Count
            || playerIds.Distinct().Count() != playerIds.Count
            || Players.Any(x => !playerIds.Contains(x.PlayerId)))
        {
            return LobbyStatus.OrderMismatch;
        }

        var positions = new Dictionary<Guid, int>(playerIds.Count);
        for (var position = 0; position < playerIds.Count; position++)
        {
            positions[playerIds[position]] = position;
        }

        foreach (var player in Players)
        {
            player.Order = positions[player.PlayerId];
        }

        return LobbyStatus.Done;
    }
}
