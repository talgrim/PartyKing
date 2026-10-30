using System.Text.Json.Serialization;

namespace PartyKing.Contract.V1.Games;

/// <summary>
/// The session's Resistance game. <see cref="CreatedAt"/> tells one game from the next, <see cref="IsOpen"/> is whether
/// guests may join, <see cref="Players"/> the players in seating order and <see cref="YourPlayerId"/> the caller's own
/// player id (null when the caller only watches). <see cref="MaxPlayers"/> is how many may join.
/// Once <see cref="StartedAt"/> is set the players are locked and <see cref="LeaderPlayerId"/> leads the mission.
/// The roles are secret: <see cref="YourRole"/> is set only for a player of a started game, and
/// <see cref="FellowSpies"/> (the other spies' player ids) only for a spy. Observers get neither.
/// The leader picks <see cref="RequiredTeamSize"/> players for mission <see cref="MissionNumber"/> (null before the
/// start); <see cref="TeamPlayerIds"/> is the submitted team, empty until then, and public.
/// <see cref="Phase"/> is the step of the round (null before the start). While the players vote only
/// <see cref="VotedPlayerIds"/> (who has voted, not how) and the caller's own <see cref="YourVote"/> are known; the
/// <see cref="Votes"/> of everybody and <see cref="VoteApproved"/> appear once all have voted. <see cref="RejectedTeams"/>
/// counts the teams rejected in a row; the fifth ends the game with the <see cref="Winner"/>, and then
/// <see cref="RevealedRoles"/> shows every role.
/// An approved team plays the mission: its members each play a secret card, <see cref="PlayedMissionPlayerIds"/> says
/// who has (not which card) and <see cref="YourMissionCard"/> is the caller's own. Once all have played only the
/// number of Fail cards of each finished mission is public (<see cref="MissionResults"/>), never who played them;
/// <see cref="RequiredFails"/> is how many of them fail the current mission. Three successes or three failures win.
/// In the Spotlight forces one team member to play openly: <see cref="OpenMissionPlayerId"/> is that player and
/// <see cref="OpenMissionCard"/> their card (true = Success), public once they have played it.
/// In the leader window (a Strong Leader is out there) <see cref="LeaderWindowPassedPlayerIds"/> lists who confirmed.
/// </summary>
public record ResistanceGameDto(
    DateTimeOffset CreatedAt,
    bool IsOpen,
    IReadOnlyList<ResistancePlayerDto> Players,
    Guid? YourPlayerId,
    int MaxPlayers,
    DateTimeOffset? StartedAt,
    Guid? LeaderPlayerId,
    ResistanceRoleDto? YourRole,
    IReadOnlyList<Guid>? FellowSpies,
    int MissionNumber,
    int? RequiredTeamSize,
    IReadOnlyList<Guid> TeamPlayerIds,
    ResistancePhaseDto? Phase,
    int RejectedTeams,
    ResistanceRoleDto? Winner,
    IReadOnlyList<Guid> VotedPlayerIds,
    bool? YourVote,
    IReadOnlyList<ResistanceVoteDto>? Votes,
    bool? VoteApproved,
    IReadOnlyList<ResistanceRevealedRoleDto>? RevealedRoles,
    ResistanceRulesDto Rules,
    int? RequiredFails,
    IReadOnlyList<Guid> PlayedMissionPlayerIds,
    bool? YourMissionCard,
    IReadOnlyList<ResistanceMissionResultDto> MissionResults,
    IReadOnlyList<ResistancePlotCardDto> YourPlotCards,
    IReadOnlyList<ResistancePlotCardCountDto> PlotCardCounts,
    IReadOnlyList<Guid> OpinionMakerPlayerIds,
    IReadOnlyList<ResistanceVoteDto> OpinionMakerVotes,
    IReadOnlyList<ResistancePlayedPlotCardDto> PlayedPlotCards,
    IReadOnlyList<ResistanceCardRevealDto> YourReveals,
    Guid? OpenMissionPlayerId,
    bool? OpenMissionCard,
    IReadOnlyList<Guid> LeaderWindowPassedPlayerIds);

/// <summary>
/// A plot card that was played; public, newest first. The identity card it showed stays with its viewer.
/// <paramref name="TargetPlayerId"/> is the neighbour (Overheard Conversation) or the player it was played on, and
/// null for a card without a target.
/// </summary>
public record ResistancePlayedPlotCardDto(
    int Id, ResistancePlotCardTypeDto Type, Guid HolderPlayerId, Guid? TargetPlayerId, int MissionNumber, bool IsConfirmed);

/// <summary>The identity card a played card shows the caller, until they confirm having seen it.</summary>
public record ResistanceCardRevealDto(int CardId, Guid PlayerId, ResistanceRoleDto Role);

/// <summary>A plot card the caller holds. <paramref name="ToDeal"/> is true while the leader still has to hand it out.</summary>
public record ResistancePlotCardDto(int Id, ResistancePlotCardTypeDto Type, bool ToDeal, bool InPlay);

/// <summary>How many plot cards a player holds in hand; public, the cards themselves are not.</summary>
public record ResistancePlotCardCountDto(Guid PlayerId, int Count);

/// <summary>Serialized by name, which the UI compares against.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResistancePlotCardTypeDto
{
    OverheardConversation,
    OpinionMaker,
    EstablishConfidence,
    OpenUp,
    NoConfidence,
    KeepingCloseEye,
    StrongLeader,
    InTheSpotlight,
    TakeResponsibility,
}

/// <summary>The leader gives the card with this id to the player.</summary>
public record ResistancePlotCardAssignmentDto(int CardId, Guid PlayerId);

/// <summary>Every drawn card except Establish Confidence (the leader keeps it) goes to a player other than the leader.</summary>
public record ResistanceDealPlotCardsRequest(IReadOnlyList<ResistancePlotCardAssignmentDto> Assignments);

/// <summary>The public outcome of a finished mission: how many Fail cards were played, not by whom.</summary>
public record ResistanceMissionResultDto(int MissionNumber, bool Succeeded, int FailCount);

/// <summary>
/// The numbers the game was created with (copied from the server configuration, refreshed on request until the game
/// is open): for every player count the spies, the team size of each mission and the fails each mission needs.
/// </summary>
/// <param name="PlotCardsEnabled">Whether the plot cards add-on is played in this game.</param>
/// <param name="PlotCardsPerRound">How many plot cards the leader draws before each mission, at the current player count.</param>
public record ResistanceRulesDto(
    IReadOnlyList<ResistanceCountRulesDto> PlayerCounts, bool PlotCardsEnabled, int PlotCardsPerRound);

public record ResistanceCountRulesDto(
    int PlayerCount, int Spies, IReadOnlyList<int> TeamSizes, IReadOnlyList<int> RequiredFails);

/// <summary>Serialized by name, which the UI compares against.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResistancePhaseDto
{
    SelectingTeam,
    Voting,
    VoteResult,
    GameOver,
    PlayingMission,
    MissionResult,
    DealingCards,
    ResolvingCards,
    LeaderWindow,
}

/// <summary>How one player voted on the team; public once everybody has voted.</summary>
public record ResistanceVoteDto(Guid PlayerId, bool Approve);

/// <summary>A player's role, revealed when the game is over.</summary>
public record ResistanceRevealedRoleDto(Guid PlayerId, ResistanceRoleDto Role);

/// <summary>Serialized by name ("Resistance", "Spy"), which the UI compares against.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResistanceRoleDto
{
    Resistance,
    Spy,
}

/// <summary>A player of the game; <see cref="Name"/> carries the "(host)" suffix for the session host.</summary>
public record ResistancePlayerDto(Guid Id, string Name);

/// <summary>
/// What any guest may know of the game: whether it is open to join, whether it started (nobody can join then) and
/// whether the caller already joined.
/// </summary>
public record ResistanceLobbyDto(DateTimeOffset CreatedAt, bool IsOpen, bool IsStarted, bool Joined);

public record ResistanceJoinRequest(string? Name);

/// <summary>The ids of all the players in the new seating order.</summary>
public record ResistancePlayersOrderRequest(IReadOnlyList<Guid>? PlayerIds);

/// <summary>The ids of the players the leader sends on the mission.</summary>
public record ResistanceTeamRequest(IReadOnlyList<Guid>? PlayerIds);

/// <summary>Switches the plot cards add-on on or off (only before the game is opened).</summary>
public record ResistancePlotCardsRequest(bool Enabled);

/// <summary>The player an immediate plot card is played on.</summary>
public record ResistancePlayPlotCardRequest(Guid TargetPlayerId);

/// <summary>
/// The player Take Responsibility takes a card from. <paramref name="TakeOpinionMaker"/> picks the face-up Opinion
/// Maker (true) or a random card from the hand (false); needed only when the target has both.
/// </summary>
public record ResistanceTakeResponsibilityRequest(Guid TargetPlayerId, bool? TakeOpinionMaker);

/// <summary>The caller's vote on the submitted team: true approves, false rejects.</summary>
public record ResistanceVoteRequest(bool Approve);

/// <summary>The card a team member plays on the mission: true is Success, false is Fail.</summary>
public record ResistanceMissionCardRequest(bool Success);
