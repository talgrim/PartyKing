namespace PartyKing.Domain.Entities.Games;

/// <summary>
/// The Resistance game of one session (at most one). An admin opens it, guests join by name, and the host or an admin
/// starts it once at least <see cref="MinPlayers"/> joined: that deals the roles (see <see cref="ResistanceRoles"/>),
/// picks the first mission leader and locks the players.
/// </summary>
public class ResistanceGame : LobbyGame<ResistancePlayer>
{
    public const int MinPlayers = 5;
    public const int MaxPlayers = 10;

    /// <summary>Null while the game is still a lobby.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>The player leading the current mission; null until the game is started.</summary>
    public Guid? LeaderPlayerId { get; set; }

    /// <summary>The mission being played, from 1; the leader picks its team (see <see cref="ResistanceRoles.TeamSize"/>).</summary>
    public int MissionNumber { get; set; } = 1;

    /// <summary>The step of the current round; <see cref="ResistancePhase.SelectingTeam"/> until the leader submits a team.</summary>
    public ResistancePhase Phase { get; set; } = ResistancePhase.SelectingTeam;

    /// <summary>Teams rejected in a row during the current mission; five of them make the spies win.</summary>
    public int RejectedTeams { get; set; }

    /// <summary>The winning side once <see cref="Phase"/> is <see cref="ResistancePhase.GameOver"/>.</summary>
    public ResistanceRole? Winner { get; set; }

    public bool IsStarted => StartedAt is not null;

    /// <summary>The leader submitted a team for the current mission, which locks it (<see cref="ResistancePlayer.IsOnTeam"/>).</summary>
    public bool IsTeamSubmitted => Players.Any(x => x.IsOnTeam);
}
