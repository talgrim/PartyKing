namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>The caller's vote on the submitted team: true approves, false rejects.</summary>
public record ResistanceVoteRequestDto(bool Approve);
