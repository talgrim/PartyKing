using PartyKing.Domain.Entities.Games.Base;
using PartyKing.Domain.Events;
using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>
/// The Resistance game of one session (at most one). An admin opens it, guests join by name, and the host or an admin
/// starts it once at least <see cref="MinPlayers"/> joined: that deals the roles (see <see cref="ResistanceDealer"/>),
/// picks the first mission leader and locks the players.
/// </summary>
public partial class ResistanceGame : LobbyGame<ResistancePlayer>
{
    // EF and the tests; a game is made through Create
    internal ResistanceGame()
    {
    }

    public static ResistanceGame Create(Guid sessionId, DateTimeOffset createdAt, ResistanceRules rules)
    {
        var game = new ResistanceGame { SessionId = sessionId, CreatedAt = createdAt, Rules = rules };
        game.Raise(new ResistanceCreated(sessionId));

        return game;
    }

    public override GameKind Kind => GameKind.Resistance;

    public const int MinPlayers = 5;
    public const int MaxPlayers = 10;

    /// <summary>Null while the game is still a lobby.</summary>
    public DateTimeOffset? StartedAt { get; internal set; }

    /// <summary>The player leading the current mission; null until the game is started.</summary>
    public Guid? LeaderPlayerId { get; internal set; }

    /// <summary>The mission being played, from 1; the leader picks its team (see <see cref="ResistanceRules.TeamSize"/>).</summary>
    public int MissionNumber { get; internal set; } = 1;

    /// <summary>The step of the current round; <see cref="ResistancePhase.SelectingTeam"/> until the leader submits a team.</summary>
    public ResistancePhase Phase { get; internal set; } = ResistancePhase.SelectingTeam;

    /// <summary>
    /// While <see cref="ResistancePhase.LeaderWindow"/>: whether the leader still draws their plot cards once it ends
    /// (a new mission) or picks the team at once (after a rejected team).
    /// </summary>
    public bool LeaderWindowBeforeDraw { get; internal set; }

    /// <summary>Teams rejected in a row during the current mission; five of them make the spies win.</summary>
    public int RejectedTeams { get; internal set; }

    /// <summary>The winning side once <see cref="Phase"/> is <see cref="ResistancePhase.GameOver"/>.</summary>
    public ResistanceRole? Winner { get; internal set; }

    /// <summary>
    /// The numbers of this game (spies, team sizes, fails needed), copied from the server configuration when the game
    /// is created and refreshed on request until it is open.
    /// </summary>
    public ResistanceRules Rules { get; internal set; } = ResistanceRules.Default();

    /// <summary>The outcomes of the missions finished so far, public.</summary>
    public List<ResistanceMissionResult> MissionResults { get; internal set; } = [];

    /// <summary>Successful missions so far; three of them win the game for the resistance.</summary>
    public int ResistanceScore => MissionResults.Count(x => x.Succeeded);

    /// <summary>Failed missions so far; three of them win the game for the spies.</summary>
    public int SpyScore => MissionResults.Count(x => !x.Succeeded);

    public bool IsStarted => StartedAt is not null;

    /// <summary>
    /// Whether the current mission is resolved and "Dalej" has not moved on yet, so its cards are still on the table. It
    /// follows from the state (a result exists for the current mission, and the number only advances on "Dalej"), not
    /// from a list of phases, so a step added between the result and "Dalej" needs no change here. A game that ended
    /// with the mission has nothing left to do with the cards.
    /// </summary>
    public bool IsMissionResolved =>
        Phase != ResistancePhase.GameOver && MissionResults.Any(x => x.MissionNumber == MissionNumber);

    protected override int PlayerLimit => MaxPlayers;

    /// <summary>Lets <see cref="ResistanceFlow"/> and <see cref="ResistanceDealer"/>, which do the transitions, raise events.</summary>
    internal void Publish(IDomainEvent domainEvent)
    {
        Raise(domainEvent);
    }

    /// <summary>Hands the lead to the given player and says so.</summary>
    internal void ChangeLeader(Guid? leaderPlayerId)
    {
        LeaderPlayerId = leaderPlayerId;
        Raise(new ResistanceLeaderChanged(SessionId, leaderPlayerId));
    }

    /// <summary>A started game locks its players.</summary>
    protected override bool IsLocked => IsStarted;

    /// <summary>The leader submitted a team for the current mission, which locks it (<see cref="ResistancePlayer.IsOnTeam"/>).</summary>
    public bool IsTeamSubmitted => Players.Any(x => x.IsOnTeam);

    /// <summary>
    /// Replaces the rules with the given ones, keeping which modules are on. Only while the game is still
    /// closed and not started, so the players never see the numbers change under them.
    /// </summary>
    public ResistanceOutcome RefreshRules(ResistanceRules rules)
    {
        if (IsOpen || IsStarted)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.RulesLocked, this);
        }

        // A copy, so the caller's object is neither changed nor shared with the game
        Rules = Rules.WithNumbersOf(rules);

        return Succeeded();
    }

    /// <summary>
    /// Starts the open game: deals the roles and picks the first leader (see <see cref="ResistanceDealer"/>), which
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

        if (ResistanceModules.StartFailure(this) is { } invalid)
        {
            return ResistanceOutcome.Failed(invalid, this);
        }

        ResistanceDealer.Deal(this, random);
        StartedAt = startedAt;
        Raise(new ResistanceStarted(SessionId));

        return Succeeded();
    }

    /// <summary>Starts a finished game over with the same players: new roles and leader, mission 1, no results.</summary>
    public ResistanceOutcome Restart()
    {
        if (!IsStarted)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotStarted, this);
        }

        // A game waiting for the Assassin can be restarted too, so an absent Assassin never strands it
        if (!ResistancePhases.CanRestart(Phase))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotOver, this);
        }

        ResistanceDealer.Reset(this);
        StartedAt = null;
        Raise(new ResistanceRestarted(SessionId));

        return Succeeded();
    }

    /// <summary>
    /// The outcome of an action that happened. Every one raises <see cref="ResistanceChanged"/>, so a listener that
    /// only wants to know the game changed need not know each action's own event.
    /// </summary>
    private ResistanceOutcome Succeeded()
    {
        Raise(new ResistanceChanged(SessionId));

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
