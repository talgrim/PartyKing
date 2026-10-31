namespace PartyKing.Contract.V1.Games;

/// <summary>The caller's vote on the submitted team: true approves, false rejects.</summary>
public record ResistanceVoteRequest(bool Approve);
