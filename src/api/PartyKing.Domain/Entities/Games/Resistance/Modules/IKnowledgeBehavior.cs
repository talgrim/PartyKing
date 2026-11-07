namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>
/// A module that changes who knows which players are spies. <see cref="ResistanceKnowledge.KnowsSpy"/> asks every
/// module that implements this, in two steps, so the one order that matters is explicit:
/// <list type="number">
/// <item>The modules that decide outright (<see cref="DecidesKnowsSpy"/>), in the order of
/// <see cref="ResistanceModules.Registered"/>, the first with an opinion wins: the Blind Spies variant (nobody knows
/// anybody) and then the Commander (who knows every spy but Deep Cover). They come before the hiders because a
/// character that hides from the other spies, a Blind Spy, does not hide from the Commander.</item>
/// <item>The modules that hide a player (<see cref="HidesSpyFrom"/>): if any of them does, the viewer does not know the
/// target. These answer the same whatever the order.</item>
/// </list>
/// What is left is the default: two spies know each other. The checks are on the characters, which are dealt only by
/// a module that is on, so a module that is off changes nothing; a module whose rule is a setting (the Blind Spies
/// variant, the hidden Reverser) checks the setting.
/// </summary>
internal interface IKnowledgeBehavior : IResistanceModuleBehavior
{
    /// <summary>
    /// Whether the viewer, who is not the target, knows the target to be a spy, when this module settles it whatever
    /// the other modules say; null when it has no opinion.
    /// </summary>
    bool? DecidesKnowsSpy(ResistanceGame game, ResistancePlayer viewer, ResistancePlayer target)
    {
        return null;
    }

    /// <summary>Whether this module keeps the viewer from knowing the target to be a spy (or the target from being known by the viewer).</summary>
    bool HidesSpyFrom(ResistanceGame game, ResistancePlayer viewer, ResistancePlayer target)
    {
        return false;
    }

    /// <summary>Whether the viewer gets a list of spies although they are not a spy (what is in it is up to the same two steps).</summary>
    bool SeesSpies(ResistancePlayer viewer)
    {
        return false;
    }
}
