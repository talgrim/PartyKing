namespace PartyKing.Domain.Entities.Games.Base;

/// <summary>A person who joined a game lobby by name. The turn order is <see cref="Order"/>.</summary>
public abstract class LobbyPlayer
{
    // EF and the derived players; a player is made through its Create factory
    protected LobbyPlayer()
    {
    }

    protected void Initialize(Guid playerId, string name, string? profileId, DateTimeOffset joinedAt, bool isAdminSeat)
    {
        PlayerId = playerId;
        Name = name;
        ProfileId = profileId;
        JoinedAt = joinedAt;
        IsAdminSeat = isAdminSeat;
    }

    public Guid SessionId { get; internal set; }
    public Guid PlayerId { get; internal set; }

    /// <summary>The name shown to everyone, without the "(host)" suffix.</summary>
    public string Name { get; internal set; } = string.Empty;

    // Not named after the property on purpose: EF would pick a matching field and bypass the getter
    private string? _normalized;

    /// <summary>
    /// The upper-cased display name, unique within the game. Derived from the name unless a stored value was set,
    /// so a player that never went through <c>Join</c> still has one.
    /// </summary>
    public string NormalizedName
    {
        get { return _normalized ?? LobbyNames.Normalize(DisplayName); }
        internal set { _normalized = value; }
    }

    /// <summary>Position in the player order, from 0.</summary>
    public int Order { get; internal set; }

    public DateTimeOffset JoinedAt { get; internal set; }

    /// <summary>The Spotify profile id of a player who joined as the session host; null for anyone else.</summary>
    public string? ProfileId { get; internal set; }

    /// <summary>Whether an admin joined as this player (whatever name they typed).</summary>
    public bool IsAdminSeat { get; internal set; }

    /// <summary>
    /// The seat of the session host or of an admin. Their identity comes from their own cookies, so such a seat is
    /// never handed to a guest.
    /// </summary>
    public bool IsManagerSeat => ProfileId is not null || IsAdminSeat;

    /// <summary>
    /// Null until the seat is taken over by another device; then a fresh id, which the player's cookie has to carry.
    /// A cookie with another stamp (the one of the device that lost the seat) no longer identifies the player.
    /// </summary>
    public Guid? ClaimStamp { get; internal set; }

    /// <summary>Whether a device that holds the given stamp (null for a cookie that never had one) is this player's.</summary>
    public bool IsHeldBy(Guid? stamp) => ClaimStamp == stamp;

    public const string HostSuffix = " (host)";

    /// <summary>The name with the "(host)" suffix for the session host; what flips and the board show.</summary>
    public string DisplayName => ProfileId is null ? Name : Name + HostSuffix;
}
