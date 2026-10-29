namespace PartyKing.Domain.Entities.Games;

/// <summary>A person who joined a Resistance game by name. The seating order is <see cref="LobbyPlayer.Order"/>.</summary>
public class ResistancePlayer : LobbyPlayer
{
    /// <summary>Null until the game is started.</summary>
    public ResistanceRole? Role { get; set; }

    /// <summary>Whether the leader put the player on the team of the current mission.</summary>
    public bool IsOnTeam { get; set; }
}
