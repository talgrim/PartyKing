using PartyKing.Domain.Entities.Games.Resistance.Modules;

namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>
/// What a player is allowed to know about another player's secret. Every view that shows a player something about
/// somebody else asks here, so a module that changes who knows whom (Blind Spies, Reverser and Defector today; the
/// Commander and the Chiefs later) changes one place, and a leak is one test table away.
/// </summary>
public static class ResistanceKnowledge
{
    /// <summary>
    /// The side the player is on right now, which is what an identity card, an Inquisitor examination or a spy's
    /// knowledge shows: the allegiance, never the character. <see cref="ResistancePlayer.Role"/> holds it; a module that
    /// changes the allegiance during the game (the Defector) changes it there. Null until the game is started.
    /// </summary>
    public static ResistanceRole? Loyalty(ResistancePlayer player)
    {
        return player.Role;
    }

    /// <summary>
    /// The character the viewer is told about on their own card: only in a started game with a module that deals
    /// characters on (a game without one has only standard players, which would only show an empty card), and never
    /// anybody else's. Null for an observer.
    /// </summary>
    public static ResistanceCharacter? OwnCharacter(ResistanceGame game, ResistancePlayer? viewer)
    {
        if (!game.IsStarted || viewer is null || !ResistanceModules.Dealing.Any(x => x.DealsCharacters(game.Rules)))
        {
            return null;
        }

        return viewer.Character;
    }

    /// <summary>
    /// Whether the viewer gets a list of spies at all: a spy does, and so does the Commander. What is in the list is up
    /// to <see cref="KnowsSpy"/>.
    /// </summary>
    public static bool SeesSpies(ResistancePlayer viewer)
    {
        return Loyalty(viewer) == ResistanceRole.Spy || ResistanceModules.Knowledge.Any(x => x.SeesSpies(viewer));
    }

    /// <summary>Whether the viewer gets a list of Commander candidates at all: only the Body Guard of an Assassin game.</summary>
    public static bool SeesCommanderCandidates(ResistanceGame game, ResistancePlayer viewer)
    {
        return game.Rules.AssassinEnabled && viewer.Character == ResistanceCharacter.BodyGuard;
    }

    /// <summary>
    /// Whether the viewer knows the target to be a spy: the spies know each other at the start of the game, unless a
    /// module changes that (Blind Spies, the Reverser who keeps their eyes closed, the Defectors, the Commander and the
    /// Blind Spy of the Assassin). The modules answer in the two steps of <see cref="IKnowledgeBehavior"/>. Nobody
    /// learns from this about their own role; the viewer has it on their card.
    /// </summary>
    public static bool KnowsSpy(ResistanceGame game, ResistancePlayer viewer, ResistancePlayer target)
    {
        if (viewer.PlayerId == target.PlayerId)
        {
            return false;
        }

        foreach (var module in ResistanceModules.Knowledge)
        {
            if (module.DecidesKnowsSpy(game, viewer, target) is { } decided)
            {
                return decided;
            }
        }

        if (ResistanceModules.Knowledge.Any(x => x.HidesSpyFrom(game, viewer, target)))
        {
            return false;
        }

        return Loyalty(viewer) == ResistanceRole.Spy && Loyalty(target) == ResistanceRole.Spy;
    }

    /// <summary>
    /// Whether the Body Guard (the viewer) takes the target for the Commander: the Commander and the False Commander look
    /// alike to them, so the Body Guard only learns that the Commander is one of the players this returns true for.
    /// </summary>
    public static bool KnowsCommander(ResistanceGame game, ResistancePlayer viewer, ResistancePlayer target)
    {
        return game.Rules.AssassinEnabled
               && viewer.Character == ResistanceCharacter.BodyGuard
               && target.Character is ResistanceCharacter.Commander or ResistanceCharacter.FalseCommander;
    }

    /// <summary>
    /// Whether the viewer knows the target to be the other Defector: only with the variant on
    /// (<see cref="ResistanceRules.DefectorsKnowEachOther"/>), and then whatever side each is on at the moment.
    /// </summary>
    public static bool KnowsDefector(ResistanceGame game, ResistancePlayer viewer, ResistancePlayer target)
    {
        return game.Rules is { DefectorEnabled: true, DefectorsKnowEachOther: true }
               && viewer.PlayerId != target.PlayerId
               && viewer.Character == ResistanceCharacter.Defector
               && target.Character == ResistanceCharacter.Defector;
    }

    /// <summary>Whether the player is the Defector who started on the resistance side (see <see cref="DefectorModuleBehavior.IsUnknown"/>).</summary>
    public static bool IsUnknownDefector(ResistanceGame game, ResistancePlayer player)
    {
        return DefectorModuleBehavior.IsUnknown(game, player);
    }

    /// <summary>Whether the player is a spy Reverser who keeps their eyes closed in the reveal (see <see cref="ReverserModuleBehavior.IsHiddenSpy"/>).</summary>
    public static bool IsHiddenReverserSpy(ResistanceGame game, ResistancePlayer player)
    {
        return ReverserModuleBehavior.IsHiddenSpy(game, player);
    }
}
