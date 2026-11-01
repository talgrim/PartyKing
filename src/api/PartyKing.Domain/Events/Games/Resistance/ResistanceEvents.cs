using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Domain.Events.Games.Resistance;

// None of these carries a secret: who voted is public, how they voted is not, and a mission card is never named.

/// <summary>The roles were dealt and the first leader picked.</summary>
public sealed record ResistanceStarted(Guid SessionId) : IDomainEvent;

/// <summary>A finished game went back to the open lobby with the same players.</summary>
public sealed record ResistanceRestarted(Guid SessionId) : IDomainEvent;

/// <summary>The leader submitted the team of the mission and it went to the vote.</summary>
public sealed record ResistanceTeamSubmitted(Guid SessionId, int MissionNumber, Guid LeaderPlayerId, IReadOnlyList<Guid> TeamPlayerIds)
    : IDomainEvent;

/// <summary>A player cast or changed their vote; the vote itself is not part of the event.</summary>
public sealed record ResistanceVoteCast(Guid SessionId, Guid PlayerId) : IDomainEvent;

/// <summary>The vote approved the team; a No Confidence card may still cancel it.</summary>
public sealed record ResistanceTeamApproved(Guid SessionId, int MissionNumber) : IDomainEvent;

/// <summary>The team was rejected, by the vote or by a No Confidence card; <c>RejectedTeams</c> is the count after it.</summary>
public sealed record ResistanceTeamRejected(Guid SessionId, int MissionNumber, int RejectedTeams) : IDomainEvent;

/// <summary>A team member played a mission card; which one stays secret.</summary>
public sealed record ResistanceMissionCardPlayed(Guid SessionId, Guid PlayerId) : IDomainEvent;

/// <summary>The last mission card was played and the mission was resolved.</summary>
public sealed record ResistanceMissionCompleted(Guid SessionId, int MissionNumber, bool Succeeded, int FailCount) : IDomainEvent;

/// <summary>The lead passed to another player.</summary>
public sealed record ResistanceLeaderChanged(Guid SessionId, Guid? LeaderPlayerId) : IDomainEvent;

/// <summary>Five teams were rejected or three missions were decided.</summary>
public sealed record ResistanceGameOver(Guid SessionId, ResistanceRole Winner) : IDomainEvent;
