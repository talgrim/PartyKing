namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>
/// The session's Resistance game as one caller may see it. Hidden information (roles, votes, mission cards, plot
/// cards, revealed identities) is filled in only for the people it belongs to; those properties start with "Your".
/// </summary>
public class ResistanceGameDto
{
    /// <summary>Tells one game from the next.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Whether guests may join.</summary>
    public bool IsOpen { get; set; }

    /// <summary>The players in seating order.</summary>
    public IReadOnlyList<ResistancePlayerDto> Players { get; set; } = [];

    /// <summary>The caller's own player id; null when the caller only watches.</summary>
    public Guid? YourPlayerId { get; set; }

    /// <summary>How many players may join.</summary>
    public int MaxPlayers { get; set; }

    /// <summary>When the game started; null while it is still a lobby. Once set, the players are locked.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>The player who leads the mission and picks the team.</summary>
    public Guid? LeaderPlayerId { get; set; }

    /// <summary>The caller's secret role. Set only for a player of a started game; observers get null.</summary>
    public ResistanceRoleDto? YourRole { get; set; }

    /// <summary>The other spies' player ids. Set only for a spy; everybody else gets null.</summary>
    public IReadOnlyList<Guid>? FellowSpies { get; set; }

    /// <summary>The number of the mission being played.</summary>
    public int MissionNumber { get; set; }

    /// <summary>How many players the leader picks for the mission; null before the start.</summary>
    public int? RequiredTeamSize { get; set; }

    /// <summary>The submitted team. Public; empty until the leader submits it.</summary>
    public IReadOnlyList<Guid> TeamPlayerIds { get; set; } = [];

    /// <summary>The step of the round; null before the start.</summary>
    public ResistancePhaseDto? Phase { get; set; }

    /// <summary>How many teams were rejected in a row. The fifth ends the game with the spies as <see cref="Winner"/>.</summary>
    public int RejectedTeams { get; set; }

    /// <summary>The winning side; null while the game is on.</summary>
    public ResistanceRoleDto? Winner { get; set; }

    /// <summary>Who has voted on the team, not how.</summary>
    public IReadOnlyList<Guid> VotedPlayerIds { get; set; } = [];

    /// <summary>The caller's own vote (true = approve); null if they have not voted.</summary>
    public bool? YourVote { get; set; }

    /// <summary>Everybody's vote. Appears once all have voted and the vote is over; null before that.</summary>
    public IReadOnlyList<ResistanceVoteDto>? Votes { get; set; }

    /// <summary>Whether the team was approved. Appears together with <see cref="Votes"/>.</summary>
    public bool? VoteApproved { get; set; }

    /// <summary>Every player's role. Appears only when the game is over.</summary>
    public IReadOnlyList<ResistanceRevealedRoleDto>? RevealedRoles { get; set; }

    /// <summary>The rules this game plays by (role counts, team sizes, required fails, plot cards).</summary>
    public required ResistanceRulesDto Rules { get; set; }

    /// <summary>How many Fail cards fail the current mission; null before the start.</summary>
    public int? RequiredFails { get; set; }

    /// <summary>Who has played a mission card, not which card.</summary>
    public IReadOnlyList<Guid> PlayedMissionPlayerIds { get; set; } = [];

    /// <summary>The caller's own mission card (true = Success); null if they have not played one.</summary>
    public bool? YourMissionCard { get; set; }

    /// <summary>
    /// The finished missions: only the number of Fail cards is public, never who played them. Three successes or
    /// three failures win.
    /// </summary>
    public IReadOnlyList<ResistanceMissionResultDto> MissionResults { get; set; } = [];

    /// <summary>The plot cards in the caller's hand (or still to be handed out), never anybody else's.</summary>
    public IReadOnlyList<ResistancePlotCardDto> YourPlotCards { get; set; } = [];

    /// <summary>How many plot cards each player holds; the hand sizes are public, the cards are not.</summary>
    public IReadOnlyList<ResistancePlotCardCountDto> PlotCardCounts { get; set; } = [];

    /// <summary>The players with an Opinion Maker in play. Public.</summary>
    public IReadOnlyList<Guid> OpinionMakerPlayerIds { get; set; } = [];

    /// <summary>The Opinion Makers' votes. Filled once every one of them has voted, before the others may.</summary>
    public IReadOnlyList<ResistanceVoteDto> OpinionMakerVotes { get; set; } = [];

    /// <summary>The public log of played plot cards, newest first.</summary>
    public IReadOnlyList<ResistancePlayedPlotCardDto> PlayedPlotCards { get; set; } = [];

    /// <summary>The identity cards shown to the caller that they have not confirmed yet.</summary>
    public IReadOnlyList<ResistanceCardRevealDto> YourReveals { get; set; } = [];

    /// <summary>The team member In the Spotlight forces to play their mission card openly this mission.</summary>
    public Guid? OpenMissionPlayerId { get; set; }

    /// <summary>The forced player's card (true = Success). Public once they have played it.</summary>
    public bool? OpenMissionCard { get; set; }

    /// <summary>
    /// Who confirmed in the leader, confidence or peek window (a playable plot card is out there), never who holds a card.
    /// </summary>
    public IReadOnlyList<Guid> LeaderWindowPassedPlayerIds { get; set; } = [];

    /// <summary>The mission cards the caller looked at with Keeping a Close Eye this mission.</summary>
    public IReadOnlyList<ResistanceMissionPeekDto> YourPeeks { get; set; } = [];
}
