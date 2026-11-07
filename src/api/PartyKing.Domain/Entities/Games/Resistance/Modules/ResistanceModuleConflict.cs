namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>A module that is not switched on together with another, and what the refused switch answers with.</summary>
internal sealed record ResistanceModuleConflict(ResistanceModule With, ResistanceFailure Failure);
