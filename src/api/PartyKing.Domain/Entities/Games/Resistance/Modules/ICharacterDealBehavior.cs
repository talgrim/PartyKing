namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>
/// A module that gives players a character. Dealing one is only possible through this interface, so the module is
/// also what tells a player their own character while it is on (<see cref="ResistanceKnowledge.OwnCharacter"/>):
/// there is no second thing to remember. <see cref="ResistanceDealer"/> asks them in the order of
/// <see cref="ResistanceModules.Registered"/>.
/// </summary>
/// <remarks>
/// The order is load-bearing: each module takes "the first standard player of a side" from the shuffle, so who gets
/// which character follows from it (Reverser, Defector, Assassin), and a module that draws from the random changes what
/// every later draw gives. A game without a module must deal as before, with no extra draw;
/// <c>ResistanceKnowledgeMatrixTests</c> pins the seeded deal.
/// </remarks>
internal interface ICharacterDealBehavior : IResistanceModuleBehavior
{
    /// <summary>
    /// Gives players their characters, when the module is on. <paramref name="shuffled"/> is the shuffled players with
    /// their side already dealt; a player still without a character is <see cref="ResistanceCharacter.Standard"/>. Runs
    /// before the first leader is picked, on a game whose module state is clear (see <see cref="IDealBehavior.Reset"/>).
    /// </summary>
    void DealCharacters(ResistanceGame game, List<ResistancePlayer> shuffled, Random random);
}
