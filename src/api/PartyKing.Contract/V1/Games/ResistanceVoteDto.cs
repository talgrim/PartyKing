namespace PartyKing.Contract.V1.Games;

/// <summary>How one player voted on the team; public once everybody has voted.</summary>
public record ResistanceVoteDto(Guid PlayerId, bool Approve);
