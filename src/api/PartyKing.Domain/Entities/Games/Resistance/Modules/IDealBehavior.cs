namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>
/// A module that has something to do when the game starts or restarts: deal its characters or cards, check that the
/// game can be started with it, clear what it accumulated. <see cref="ResistanceDealer"/> and
/// <see cref="ResistanceGame.Start"/> ask every module that implements this, in the order of
/// <see cref="ResistanceModules.Registered"/>.
/// </summary>
/// <remarks>
/// The order is load-bearing for the characters: each module takes "the first standard player of a side" from the
/// shuffle, so who gets which character follows from it (Reverser, Defector, Assassin), and a module that draws from
/// the random changes what every later draw gives. A game without a module must deal as before, with no extra draw;
/// <c>ResistanceKnowledgeMatrixTests</c> pins the seeded deal.
/// </remarks>
internal interface IDealBehavior : IResistanceModuleBehavior
{
    /// <summary>
    /// Why the game cannot be started with the module on for these players (the count, the rules), or null. The first
    /// module in the order that fails answers.
    /// </summary>
    ResistanceFailure? StartFailure(ResistanceGame game)
    {
        return null;
    }

    /// <summary>
    /// Gives players their characters. <paramref name="shuffled"/> is the shuffled players with their side already
    /// dealt; a player still without a character is <see cref="ResistanceCharacter.Standard"/>. Runs before the first
    /// leader is picked.
    /// </summary>
    void DealCharacters(ResistanceGame game, List<ResistancePlayer> shuffled, Random random)
    {
    }

    /// <summary>
    /// Whether the module, as <paramref name="rules"/> have it, gives players a character that each of them is told
    /// about (<see cref="ResistanceKnowledge.OwnCharacter"/>). A module that deals characters says so here;
    /// <c>ResistanceModuleBehaviorsTests</c> fails for one that deals a character and does not.
    /// </summary>
    bool DealsCharacters(ResistanceRules rules)
    {
        return false;
    }

    /// <summary>Deals what depends on the first leader (a token, a deck). Runs after the leader is picked.</summary>
    void DealTable(ResistanceGame game, Random random)
    {
    }

    /// <summary>Clears what the module accumulated, whether it is on or not, when the game is restarted.</summary>
    void Reset(ResistanceGame game)
    {
    }
}
