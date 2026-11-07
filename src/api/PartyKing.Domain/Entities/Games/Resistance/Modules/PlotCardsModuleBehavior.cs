using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>The plot cards add-on.</summary>
internal sealed class PlotCardsModuleBehavior : IDealBehavior
{
    public ResistanceModule Module => ResistanceModule.PlotCards;

    public ResistanceFailure LockedFailure => ResistanceFailure.PlotCardsLocked;

    public bool IsOn(ResistanceRules rules)
    {
        return rules.PlotCardsEnabled;
    }

    public void Switch(ResistanceRules rules, bool on)
    {
        rules.PlotCardsEnabled = on;
    }

    /// <summary>
    /// Builds and shuffles the deck from the rules' composition when the add-on is on (after the leader is picked: it draws from the same random). The deck holds at
    /// most 15 cards and a game draws at most 5 missions x 3 cards, so it never runs out.
    /// </summary>
    public void DealTable(ResistanceGame game, Random random)
    {
        if (!game.Rules.PlotCardsEnabled)
        {
            return;
        }

        var types = game.Rules.PlotDeck
            .OrderBy(x => x.Key)
            .SelectMany(x => Enumerable.Repeat(x.Key, x.Value))
            .ToList();
        var order = Enumerable.Range(0, types.Count).Shuffled(random);

        // A restart reuses the rows (same keys), because deleting and re-adding a key in one save conflicts
        game.PlotCards.RemoveAll(x => x.CardId >= types.Count);
        for (var i = 0; i < types.Count; i++)
        {
            var card = game.PlotCards.Find(x => x.CardId == i);
            if (card is null)
            {
                card = new ResistancePlotCard { SessionId = game.SessionId, CardId = i };
                game.PlotCards.Add(card);
            }

            card.Type = types[i];
            card.Location = ResistancePlotCardLocation.Deck;
            card.HolderPlayerId = null;
            card.DeckOrder = order[i];
            card.TargetPlayerId = null;
            card.PlayedAt = null;
            card.PlayedInMission = 0;
            card.IsConfirmed = false;
        }
    }

    public void Reset(ResistanceGame game)
    {
        game.PlotCards.Clear();
    }
}
