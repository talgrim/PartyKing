namespace PartyKing.Domain.Entities.Games;

/// <summary>Who plays when: the turn goes round the players in <see cref="RingoPlayer.Order"/>.</summary>
public static class RingoTurns
{
    /// <summary>The player whose turn it is; null when nobody has joined.</summary>
    public static RingoPlayer? Current(RingoGame game)
    {
        var ordered = game.Players.OrderBy(x => x.Order).ToList();

        return ordered.FirstOrDefault(x => x.PlayerId == game.CurrentPlayerId) ?? ordered.FirstOrDefault();
    }

    /// <summary>The player after the current one, wrapping around; null when nobody has joined.</summary>
    public static RingoPlayer? Next(RingoGame game)
    {
        var ordered = game.Players.OrderBy(x => x.Order).ToList();
        if (ordered.Count == 0)
        {
            return null;
        }

        var index = ordered.FindIndex(x => x.PlayerId == game.CurrentPlayerId);

        return ordered[(index + 1) % ordered.Count];
    }

    /// <summary>
    /// The player whose turn it is once <paramref name="removed"/> has left. Someone else's turn stays theirs. When
    /// the removed player had the turn it goes to the player who followed them, unless a card of theirs awaits
    /// "continue": then it goes to the player before them, so that continuing passes it on to the follower.
    /// </summary>
    public static RingoPlayer? AfterRemoving(RingoGame game, RingoPlayer removed, bool wasCurrent)
    {
        var ordered = game.Players.OrderBy(x => x.Order).ToList();
        var index = ordered.IndexOf(removed);
        ordered.RemoveAt(index);
        if (ordered.Count == 0)
        {
            return null;
        }

        if (!wasCurrent)
        {
            return Current(game) is { } current ? ordered.First(x => x.PlayerId == current.PlayerId) : ordered[0];
        }

        return game.PendingCardId is null
            ? ordered[index % ordered.Count]
            : ordered[(index - 1 + ordered.Count) % ordered.Count];
    }
}
