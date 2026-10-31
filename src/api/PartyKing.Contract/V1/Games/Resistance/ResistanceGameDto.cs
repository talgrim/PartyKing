namespace PartyKing.Contract.V1.Games.Resistance;

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
/// <see cref="YourPeeks"/> are the mission cards the caller looked at with Keeping a Close Eye this mission.
/// In the leader or confidence window (a Strong Leader or No Confidence is out there) <see cref="LeaderWindowPassedPlayerIds"/> lists who confirmed.
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
    IReadOnlyList<Guid> LeaderWindowPassedPlayerIds,
    IReadOnlyList<ResistanceMissionPeekDto> YourPeeks);
