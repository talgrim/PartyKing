namespace PartyKing.Domain.Entities.Games.Base;

/// <summary>A person who joined a game lobby by name. The turn order is <see cref="Order"/>.</summary>
public abstract class LobbyPlayer
{
    // EF and the derived players; a player is made through its Create factory
    protected LobbyPlayer()
    {
    }

    protected void Initialize(Guid playerId, string name, string? profileId, DateTimeOffset joinedAt)
    {
        PlayerId = playerId;
        Name = name;
        ProfileId = profileId;
        JoinedAt = joinedAt;
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

    public const string HostSuffix = " (host)";

    /// <summary>The name with the "(host)" suffix for the session host; what flips and the board show.</summary>
    public string DisplayName => ProfileId is null ? Name : Name + HostSuffix;
}
