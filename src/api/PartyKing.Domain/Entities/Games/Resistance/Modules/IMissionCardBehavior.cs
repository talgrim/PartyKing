namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>
/// A module that restricts which mission card a team member may play (the Reverser card, the spy Reverser who cannot
/// fail). <see cref="ResistanceGame.PlayMissionCard"/> asks them after its own checks (a resistance member must play
/// Success), in the order of <see cref="ResistanceModules.Registered"/>, and the first failure answers.
/// </summary>
internal interface IMissionCardBehavior : IResistanceModuleBehavior
{
    /// <summary>The failure of <paramref name="player"/> playing <paramref name="card"/>, or null when the module allows it.</summary>
    ResistanceFailure? CardFailure(ResistancePlayer player, ResistanceMissionCardKind card);
}
