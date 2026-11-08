namespace PartyKing.Domain.Entities.Games.Base;

internal static class ShuffleExtensions
{
    /// <summary>A uniformly random order of the items (Fisher-Yates), taking the randomness as an argument.</summary>
    public static List<T> Shuffled<T>(this IEnumerable<T> items, Random random)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }
}
