namespace PartyKing.Domain.Events.Games.Ringo;

/// <summary>A card was turned over and is now the pending one. <c>FlippedBy</c> is the name recorded in the history.</summary>
public sealed record RingoCardFlipped(Guid SessionId, int CardId, string FlippedBy) : IDomainEvent;

/// <summary>The pending card was dealt with and the turn moved on; <c>PlayerId</c> is null when nobody has joined.</summary>
public sealed record RingoTurnPassed(Guid SessionId, Guid? PlayerId) : IDomainEvent;

/// <summary>The fourth king was flipped: nothing can be flipped any more.</summary>
public sealed record RingoGameOver(Guid SessionId, string FlippedBy) : IDomainEvent;
