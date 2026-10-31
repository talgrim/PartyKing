namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>The ids of the players the leader sends on the mission.</summary>
public record ResistanceTeamRequest(IReadOnlyList<Guid>? PlayerIds);
