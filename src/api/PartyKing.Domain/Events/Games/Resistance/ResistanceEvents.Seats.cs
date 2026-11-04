namespace PartyKing.Domain.Events.Games.Resistance;

/// <summary>The host or an admin released a seat so a guest may take it over, or took the release back.</summary>
public sealed record ResistanceSeatReleaseChanged(Guid SessionId, Guid PlayerId, bool Released) : IDomainEvent;

/// <summary>A guest took over the seat of a player who was gone; the old device lost it.</summary>
public sealed record ResistanceSeatClaimed(Guid SessionId, Guid PlayerId) : IDomainEvent;
