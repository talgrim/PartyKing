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
    ResistanceRulesDto Rules);

/// <summary>
/// The numbers the game was created with (copied from the server configuration, refreshed on request until the game
/// is open): for every player count the spies, the team size of each mission and the fails each mission needs.
/// </summary>
public record ResistanceRulesDto(IReadOnlyList<ResistanceCountRulesDto> PlayerCounts);

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

/// <summary>The caller's vote on the submitted team: true approves, false rejects.</summary>
public record ResistanceVoteRequest(bool Approve);
