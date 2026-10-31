namespace PartyKing.Contract.V1.Games;

/// <summary>
/// A plot card that was played; public, newest first. The identity card it showed stays with its viewer.
/// <paramref name="TargetPlayerId"/> is the neighbour (Overheard Conversation) or the player it was played on, and
/// null for a card without a target.
/// </summary>
public record ResistancePlayedPlotCardDto(
    int Id, ResistancePlotCardTypeDto Type, Guid HolderPlayerId, Guid? TargetPlayerId, int MissionNumber, bool IsConfirmed);
