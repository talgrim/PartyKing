using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Domain.Events.Games.Resistance;

// The events of the plot cards add-on, like ResistanceGame.PlotCards.cs. Same rule: no secrets in them.

/// <summary>A plot card was played; the log of played cards is public.</summary>
public sealed record ResistancePlotCardPlayed(
    Guid SessionId, int CardId, ResistancePlotCardType Type, Guid? HolderPlayerId, Guid? TargetPlayerId) : IDomainEvent;
