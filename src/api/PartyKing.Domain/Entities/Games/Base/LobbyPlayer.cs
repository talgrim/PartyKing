namespace PartyKing.Domain.Entities.Games.Base;

/// <summary>A person who joined a game lobby by name. The turn order is <see cref="Order"/>.</summary>
public abstract class LobbyPlayer
{
    public Guid SessionId { get; set; }
    public Guid PlayerId { get; set; }

    /// <summary>The name shown to everyone, without the "(host)" suffix.</summary>
    public string Name { get; set; } = string.Empty;

    // Not named after the property on purpose: EF would pick a matching field and bypass the getter
    private string? _normalized;

    /// <summary>
    /// The upper-cased display name, unique within the game. Derived from the name unless a stored value was set,
    /// so a player that never went through <c>Join</c> still has one.
    /// </summary>
    public string NormalizedName
    {
        get { return _normalized ?? LobbyNames.Normalize(DisplayName); }
        set { _normalized = value; }
    }

    /// <summary>Position in the player order, from 0.</summary>
    public int Order { get; set; }

    public DateTimeOffset JoinedAt { get; set; }

    /// <summary>The Spotify profile id of a player who joined as the session host; null for anyone else.</summary>
    public string? ProfileId { get; set; }

    public const string HostSuffix = " (host)";

    /// <summary>The name with the "(host)" suffix for the session host; what flips and the board show.</summary>
    public string DisplayName => ProfileId is null ? Name : Name + HostSuffix;
}
