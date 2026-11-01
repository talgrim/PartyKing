using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>
/// The Resistance game of one session (at most one). An admin opens it, guests join by name, and the host or an admin
/// starts it once at least <see cref="MinPlayers"/> joined: that deals the roles (see <see cref="ResistanceRoles"/>),
/// picks the first mission leader and locks the players.
/// </summary>
public partial class ResistanceGame : LobbyGame<ResistancePlayer>
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

    protected override int PlayerLimit => MaxPlayers;

    /// <summary>A started game locks its players.</summary>
    protected override bool IsLocked => IsStarted;

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

        // A copy, so the caller's object is neither changed nor shared with the game
        Rules = rules.Clone(Rules.PlotCardsEnabled);

        return ResistanceOutcome.Success(this);
    }

    /// <summary>
    /// Starts the open game: deals the roles and picks the first leader (see <see cref="ResistanceRoles"/>), which
    /// locks the players.
    /// </summary>
    public ResistanceOutcome Start(DateTimeOffset startedAt, Random random)
    {
        if (IsStarted)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.AlreadyStarted, this);
        }

        if (!IsOpen)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotOpen, this);
        }

        if (Players.Count < MinPlayers)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotEnoughPlayers, this);
        }

        ResistanceRoles.Deal(this, random);
        StartedAt = startedAt;

        return ResistanceOutcome.Success(this);
    }

    /// <summary>Starts a finished game over with the same players: new roles and leader, mission 1, no results.</summary>
    public ResistanceOutcome Restart()
    {
        if (!IsStarted)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotStarted, this);
        }

        if (Phase != ResistancePhase.GameOver)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotOver, this);
        }

        ResistanceRoles.Reset(this);
        StartedAt = null;

        return ResistanceOutcome.Success(this);
    }

    /// <summary>
    /// The leader submits the team of the current mission, which locks it and puts it to the vote: exactly the number
    /// of different players the rules ask for.
    /// </summary>
    public ResistanceOutcome SelectTeam(Guid? callerPlayerId, IReadOnlyList<Guid> playerIds)
    {
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        if (callerPlayerId is null || callerPlayerId != LeaderPlayerId)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotLeaderToPick, this);
        }

        if (Phase is ResistancePhase.DealingCards or ResistancePhase.ResolvingCards)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardsNotDealt, this);
        }

        if (Phase is ResistancePhase.LeaderWindow or ResistancePhase.VoteResult)
        {
            // No team has been picked in these phases, so "a team was already submitted" would be wrong
            return ResistanceOutcome.Failed(ResistanceFailure.NotSelectingTeam, this);
        }

        if (Phase != ResistancePhase.SelectingTeam)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.TeamSubmitted, this);
        }

        if (playerIds.Count != Rules.TeamSize(Players.Count, MissionNumber)
            || playerIds.Distinct().Count() != playerIds.Count)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.WrongTeamSize, this);
        }

        if (playerIds.Any(id => Players.All(x => x.PlayerId != id)))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.UnknownTeamPlayer, this);
        }

        foreach (var player in Players)
        {
            player.IsOnTeam = playerIds.Contains(player.PlayerId);
            player.Vote = null;
            player.MissionCard = null;
        }

        Phase = ResistancePhase.Voting;

        return ResistanceOutcome.Success(this);
    }

    /// <summary>
    /// A player votes on the submitted team (a repeated vote replaces the earlier one). The vote that completes the
    /// round is tallied right away: a majority approves, otherwise (a tie too) the team is rejected, which counts
    /// towards the five that make the spies win.
    /// </summary>
    public ResistanceOutcome Vote(Guid? voterId, bool approve)
    {
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        var voter = Players.FirstOrDefault(x => x.PlayerId == voterId);
        if (voter is null)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotPlayerToVote, this);
        }

        if (Phase != ResistancePhase.Voting)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotVoting, this);
        }

        var opinionMakers = ResistanceRoles.OpinionMakerIds(this);
        if (opinionMakers.Contains(voter.PlayerId))
        {
            if (voter.Vote is not null)
            {
                return ResistanceOutcome.Failed(ResistanceFailure.VoteLocked, this);
            }
        }
        else if (opinionMakers.Any(id => Players.First(x => x.PlayerId == id).Vote is null))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.OpinionMakersFirst, this);
        }

        voter.Vote = approve;
        if (Players.All(x => x.Vote is not null))
        {
            ResistanceRoles.ResolveVotes(this);
        }

        return ResistanceOutcome.Success(this);
    }

    /// <summary>
    /// A member of the approved team secretly plays a Success or Fail card; it can't be changed. The card that
    /// completes the team is resolved right away: the mission fails when the Fail cards reach the rules' requirement,
    /// and the third success or failure ends the game.
    /// </summary>
    public ResistanceOutcome PlayMissionCard(Guid? callerPlayerId, bool success)
    {
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        var player = Players.FirstOrDefault(x => x.PlayerId == callerPlayerId);
        if (player is null)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotPlayerToPlay, this);
        }

        if (Phase != ResistancePhase.PlayingMission)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NoApprovedTeam, this);
        }

        if (!player.IsOnTeam)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotOnTeam, this);
        }

        if (player.MissionCard is not null)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.AlreadyPlayed, this);
        }

        if (!success && player.Role == ResistanceRole.Resistance)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.MustSucceed, this);
        }

        player.MissionCard = success;
        if (ResistanceRoles.AllCardsPlayed(this))
        {
            ResistanceRoles.ResolveMission(this);
        }

        return ResistanceOutcome.Success(this);
    }

    /// <summary>
    /// Moves on after a finished mission (only valid in <see cref="ResistancePhase.MissionResult"/>): the next player
    /// leads, the team, the votes and the cards are cleared and the next mission starts. A rejected team needs no
    /// "continue", the lead passes at once. The leader or a manager may do it.
    /// </summary>
    public ResistanceOutcome Continue(Guid? callerPlayerId, bool isManager)
    {
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        if (!isManager && (callerPlayerId is null || callerPlayerId != LeaderPlayerId))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotAllowedToContinue, this);
        }

        if (Phase != ResistancePhase.MissionResult)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NothingToContinue, this);
        }

        ResistanceRoles.PassLeadership(this);

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
        Rules = Rules.Clone(enabled);

        return ResistanceOutcome.Success(this);
    }

    /// <summary>The failure of an action on a game that is not running (not started, or over), or null when it runs.</summary>
    private ResistanceOutcome? Inactive()
    {
        if (!IsStarted)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotStarted, this);
        }

        return Phase == ResistancePhase.GameOver
            ? ResistanceOutcome.Failed(ResistanceFailure.GameOver, this)
            : null;
    }
}
