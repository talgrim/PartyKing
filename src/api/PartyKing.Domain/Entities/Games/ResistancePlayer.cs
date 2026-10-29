namespace PartyKing.Domain.Entities.Games;

/// <summary>A person who joined a Resistance game by name. The seating order is <see cref="LobbyPlayer.Order"/>.</summary>
public class ResistancePlayer : LobbyPlayer
{
    /// <summary>Null until the game is started.</summary>
    public ResistanceRole? Role { get; set; }

    /// <summary>Whether the leader put the player on the team of the current mission.</summary>
    public bool IsOnTeam { get; set; }

    /// <summary>
    /// The player's vote on the submitted team (true approves); null until they voted. Kept after the reveal so
    /// everybody sees who voted how, and cleared when the next team is picked.
    /// </summary>
    public bool? Vote { get; set; }
}
