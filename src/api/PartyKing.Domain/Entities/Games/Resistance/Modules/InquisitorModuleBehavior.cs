namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>The Inquisitor module (Hostile Intent).</summary>
internal sealed class InquisitorModuleBehavior : IDealBehavior
{
    public ResistanceModule Module => ResistanceModule.Inquisitor;

    public ResistanceFailure LockedFailure => ResistanceFailure.InquisitorLocked;

    public bool IsOn(ResistanceRules rules)
    {
        return rules.InquisitorEnabled;
    }

    public void Switch(ResistanceRules rules, bool on)
    {
        rules.InquisitorEnabled = on;
    }

    /// <summary>The token starts with the player to the right of the first leader: the one before them in seating order.</summary>
    public void DealTable(ResistanceGame game, Random random)
    {
        if (!game.Rules.InquisitorEnabled)
        {
            return;
        }

        var seats = game.Players.OrderBy(x => x.Order).ToList();
        var holder = seats[(seats.FindIndex(x => x.PlayerId == game.LeaderPlayerId) + seats.Count - 1) % seats.Count];
        holder.WasInquisitor = true;
        game.InquisitorPlayerId = holder.PlayerId;
    }

    public void Reset(ResistanceGame game)
    {
        foreach (var player in game.Players)
        {
            player.WasInquisitor = false;
        }

        game.InquisitorPlayerId = null;
        game.InquisitionExaminerPlayerId = null;
    }
}
