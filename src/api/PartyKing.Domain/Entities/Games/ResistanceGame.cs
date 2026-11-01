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

    /// <summary>The mission being played, from 1; the leader picks its team (see <see cref="ResistanceRules.TeamSize"/>).</summary>
    public int MissionNumber { get; set; } = 1;

    /// <summary>The step of the current round; <see cref="ResistancePhase.SelectingTeam"/> until the leader submits a team.</summary>
    public ResistancePhase Phase { get; set; } = ResistancePhase.SelectingTeam;

    /// <summary>
    /// While <see cref="ResistancePhase.LeaderWindow"/>: whether the leader still draws their plot cards once it ends
    /// (a new mission) or picks the team at once (after a rejected team).
    /// </summary>
    public bool LeaderWindowBeforeDraw { get; set; }

    /// <summary>Teams rejected in a row during the current mission; five of them make the spies win.</summary>
    public int RejectedTeams { get; set; }

    /// <summary>The winning side once <see cref="Phase"/> is <see cref="ResistancePhase.GameOver"/>.</summary>
    public ResistanceRole? Winner { get; set; }

    /// <summary>
    /// The numbers of this game (spies, team sizes, fails needed), copied from the server configuration when the game
    /// is created and refreshed on request until it is open.
    /// </summary>
    public ResistanceRules Rules { get; set; } = ResistanceRules.Default();

    /// <summary>The outcomes of the missions finished so far, public.</summary>
    public List<ResistanceMissionResult> MissionResults { get; set; } = [];

    /// <summary>The plot cards of the add-on (deck, drawn and in hands); empty when the add-on is off.</summary>
    public List<ResistancePlotCard> PlotCards { get; set; } = [];

    /// <summary>Successful missions so far; three of them win the game for the resistance.</summary>
    public int ResistanceScore => MissionResults.Count(x => x.Succeeded);

    /// <summary>Failed missions so far; three of them win the game for the spies.</summary>
    public int SpyScore => MissionResults.Count(x => !x.Succeeded);

    public bool IsStarted => StartedAt is not null;

    /// <summary>The leader submitted a team for the current mission, which locks it (<see cref="ResistancePlayer.IsOnTeam"/>).</summary>
    public bool IsTeamSubmitted => Players.Any(x => x.IsOnTeam);

    /// <summary>
    /// Replaces the rules with the given ones, keeping whether the plot cards are on. Only while the game is still
    /// closed and not started, so the players never see the numbers change under them.
    /// </summary>
    public ResistanceOutcome RefreshRules(ResistanceRules rules)
    {
        if (IsOpen || IsStarted)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.RulesLocked, this);
        }

        rules.PlotCardsEnabled = Rules.PlotCardsEnabled;
        Rules = rules;

        return ResistanceOutcome.Success(this);
    }

    /// <summary>Switches the plot cards add-on on or off in the rules. Allowed until the game starts, open or not.</summary>
    public ResistanceOutcome SetPlotCards(bool enabled)
    {
        if (IsStarted)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.PlotCardsLocked, this);
        }

        // Rules is a JSON column compared by reference, so assign a copy to have the change detected.
        Rules = new ResistanceRules
        {
            PlayerCounts = Rules.PlayerCounts,
            PlotDeck = Rules.PlotDeck,
            PlotCardsEnabled = enabled,
        };

        return ResistanceOutcome.Success(this);
    }
}
