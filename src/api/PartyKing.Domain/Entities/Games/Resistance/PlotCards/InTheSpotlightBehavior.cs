namespace PartyKing.Domain.Entities.Games.Resistance.PlotCards;

/// <summary>Played while the mission is played, on a team member who has not played their mission card yet.</summary>
internal sealed class InTheSpotlightBehavior : IPlayedOnATargetBehavior
{
    public ResistancePlotCardType Type => ResistancePlotCardType.InTheSpotlight;

    public ResistancePhase PlayPhase => ResistancePhase.ConfidenceWindow;

    public ResistanceFailure WrongPlayPhase => ResistanceFailure.NotPlayingMission;

    public bool IsConfirmedOnPlay => true;

    public bool CanBePlayed(ResistanceGame game)
    {
        // One player is forced open per mission
        return ResistancePlotCardQueries.OpenMissionPlayerId(game) is null;
    }

    public bool IsValidTarget(ResistanceGame game, ResistancePlotCard card, Guid targetPlayerId)
    {
        return ResistancePlotCardQueries.CanBeForcedOpen(game, card.HolderPlayerId!.Value, targetPlayerId);
    }
}
