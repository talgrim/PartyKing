using System.Text;
using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Domain.Tests;

/// <summary>
/// Who may know what about whom, and how the characters are dealt, over every legal combination of the rules modules,
/// the optional Assassin characters, the player counts and the Defector switches. A leak here is a security bug, and the
/// modules reach <see cref="ResistanceKnowledge"/> and the dealer from several places, so the whole table is pinned
/// against a snapshot of the behaviour (<c>Golden/ResistanceKnowledgeMatrix.txt</c>) instead of re-deriving it.
/// </summary>
/// <remarks>
/// A change of behaviour fails here with the first differing scenario. When the change is intended, regenerate the
/// snapshot with <c>PARTYKING_UPDATE_GOLDEN=1 dotnet test --filter ResistanceKnowledgeMatrixTests</c> and review the diff.
/// </remarks>
public class ResistanceKnowledgeMatrixTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private const string GoldenFile = "ResistanceKnowledgeMatrix.txt";

    /// <summary>The player counts that differ in the dealt composition; the extras variant is dealt for a few only, to keep the snapshot small.</summary>
    private static readonly int[] PlayerCounts = [5, 6, 7, 8, 9, 10];

    private static readonly int[] PlayerCountsWithExtras = [5, 8, 10];

    private sealed record Combination(string Name, Action<ResistanceRules> Configure);

    private static IEnumerable<Combination> Combinations()
    {
        // Blind Spies x Reverser x Defector, none of which the Assassin combines with
        foreach (var blindSpies in new[] { false, true })
        {
            foreach (var reverser in ReverserSettings())
            {
                foreach (var defector in DefectorSettings())
                {
                    var name = $"{(blindSpies ? "BlindSpies" : "-")}/{reverser.Name}/{defector.Name}";

                    yield return new Combination(name, rules =>
                    {
                        rules.BlindSpiesEnabled = blindSpies;
                        reverser.Apply(rules);
                        defector.Apply(rules);
                    });
                }
            }
        }

        // The Assassin with every subset of the optional characters (a subset the count cannot deal is refused at the start)
        for (var subset = 0; subset < 16; subset++)
        {
            var bits = subset;
            var optional = new[] { "BodyGuard", "DeepCover", "BlindSpy", "FalseCommander" }
                .Where((_, bit) => (bits & (1 << bit)) != 0);
            var name = $"Assassin/{string.Join(',', optional)}";

            yield return new Combination(name, rules =>
            {
                rules.AssassinEnabled = true;
                rules.BodyGuardEnabled = (bits & 1) != 0;
                rules.DeepCoverEnabled = (bits & 2) != 0;
                rules.BlindSpyEnabled = (bits & 4) != 0;
                rules.FalseCommanderEnabled = (bits & 8) != 0;
            });
        }
    }

    private sealed record Setting(string Name, Action<ResistanceRules> Apply);

    private static IEnumerable<Setting> ReverserSettings()
    {
        yield return new Setting("-", _ => { });

        foreach (var sides in Enum.GetValues<ResistanceReverserSides>())
        {
            foreach (var spyKnowsSpies in new[] { true, false })
            {
                yield return new Setting($"Reverser:{sides}:{(spyKnowsSpies ? "spiesKnow" : "spiesDont")}", rules =>
                {
                    rules.ReverserEnabled = true;
                    rules.ReverserSides = sides;
                    rules.ReverserSpyKnowsSpies = spyKnowsSpies;
                });
            }
        }
    }

    private static IEnumerable<Setting> DefectorSettings()
    {
        yield return new Setting("-", _ => { });
        yield return new Setting("Defector", rules => rules.DefectorEnabled = true);
        yield return new Setting("Defector:know", rules =>
        {
            rules.DefectorEnabled = true;
            rules.DefectorsKnowEachOther = true;
        });
    }

    /// <summary>One line per scenario: the rules, the dealt state and, per seat, what the seat is and may know.</summary>
    internal static string Snapshot()
    {
        var text = new StringBuilder();

        foreach (var combination in Combinations())
        {
            foreach (var extras in new[] { false, true })
            {
                foreach (var count in extras ? PlayerCountsWithExtras : PlayerCounts)
                {
                    AppendScenario(text, combination, extras, count);
                }
            }
        }

        return text.ToString();
    }

    private static void AppendScenario(StringBuilder text, Combination combination, bool extras, int count)
    {
        var rules = ResistanceRules.Default();
        combination.Configure(rules);
        if (extras)
        {
            // The plot deck and the Inquisitor token are dealt with the same random, after the characters
            rules.PlotCardsEnabled = true;
            rules.InquisitorEnabled = true;
        }

        var game = new ResistanceGame { SessionId = Guid.NewGuid(), IsOpen = true, Rules = rules };
        for (var i = 0; i < count; i++)
        {
            game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = i });
        }

        var name = $"{combination.Name}{(extras ? "+extras" : "")}/{count}";
        var outcome = game.Start(Now, new Random(1000 + count));
        if (outcome.Failure is { } failure)
        {
            text.Append(name).Append(" => refused ").Append(failure).Append('\n');

            return;
        }

        // The Defector's switch cards turn up from the third mission on: look at every mission of the game
        var lastMission = rules.DefectorEnabled && !extras ? ResistanceRules.Missions : 1;
        for (var mission = 1; mission <= lastMission; mission++)
        {
            if (mission > 1)
            {
                ResistanceFlow.PassLeadership(game);
            }

            text.Append(name).Append(" m").Append(mission).Append(" => ").Append(Describe(game)).Append('\n');
        }
    }

    private static string Describe(ResistanceGame game)
    {
        var players = game.Players.OrderBy(x => x.Order).ToList();
        int? SeatOf(Guid? id) => id is null ? null : players.FindIndex(x => x.PlayerId == id);

        var text = new StringBuilder();
        text.Append("leader=").Append(SeatOf(game.LeaderPlayerId));
        text.Append(" inquisitor=").Append(SeatOf(game.Inquisition.HolderPlayerId)?.ToString() ?? "-");
        text.Append(" deck=").Append(game.DefectorDeck.Cards.Length == 0 ? "-" : game.DefectorDeck.Cards);
        text.Append(" switched=").Append(game.DefectorsSwitched ? 'Y' : 'N');

        var cards = game.PlotCards
            .OrderBy(x => x.DeckOrder)
            .Select(x => $"{(int)x.Type}{x.Location.ToString()[0]}{SeatOf(x.HolderPlayerId)?.ToString() ?? "-"}");
        text.Append(" plot=").Append(string.Join(',', cards));

        foreach (var viewer in players)
        {
            text.Append(" | ").Append(players.IndexOf(viewer)).Append(':');
            text.Append(viewer.Character.ToString()[..3]).Append(viewer.Role == ResistanceRole.Spy ? 'S' : 'R');

            AppendIf(text, ResistanceKnowledge.SeesSpies(viewer), " sees");
            AppendIf(text, ResistanceKnowledge.SeesCommanderCandidates(game, viewer), " candidates");
            AppendIf(text, ResistanceKnowledge.IsUnknownDefector(game, viewer), " unknownDefector");
            AppendIf(text, ResistanceKnowledge.IsHiddenReverserSpy(game, viewer), " hiddenReverser");
            AppendSeats(text, " spies", players, x => ResistanceKnowledge.KnowsSpy(game, viewer, x));
            AppendSeats(text, " commander", players, x => ResistanceKnowledge.KnowsCommander(game, viewer, x));
            AppendSeats(text, " defector", players, x => ResistanceKnowledge.KnowsDefector(game, viewer, x));
        }

        return text.ToString();
    }

    private static void AppendIf(StringBuilder text, bool condition, string flag)
    {
        if (condition)
        {
            text.Append(flag);
        }
    }

    private static void AppendSeats(
        StringBuilder text, string label, List<ResistancePlayer> players, Func<ResistancePlayer, bool> knows)
    {
        var seats = players.Select((x, seat) => (x, seat)).Where(x => knows(x.x)).Select(x => x.seat).ToList();
        if (seats.Count > 0)
        {
            text.Append(label).Append('[').Append(string.Join(',', seats)).Append(']');
        }
    }

    [Fact]
    public void TheSnapshot_IsTheSameEveryTime()
    {
        Assert.Equal(Snapshot(), Snapshot());
    }

    [Fact]
    public void TheSnapshot_CoversEveryCharacterAndEveryKindOfKnowledge()
    {
        var snapshot = Snapshot();

        foreach (var character in Enum.GetValues<ResistanceCharacter>())
        {
            var prefix = character.ToString()[..3];
            Assert.True(
                snapshot.Contains($"{prefix}S") || snapshot.Contains($"{prefix}R"),
                $"No {character} is dealt in any scenario");
        }

        foreach (var covered in new[] { " sees", " candidates", " unknownDefector", " hiddenReverser", " spies[", " commander[", " defector[", "switched=Y", "=> refused" })
        {
            Assert.Contains(covered, snapshot);
        }

        // Both sides of the Defector and the Reverser appear
        Assert.Contains("DefR", snapshot);
        Assert.Contains("DefS", snapshot);
        Assert.Contains("RevR", snapshot);
        Assert.Contains("RevS", snapshot);
    }

    [Fact]
    public void WhoKnowsWhatAndWhoIsDealtWhat_IsAsPinned()
    {
        GoldenFiles.AssertMatches(GoldenFile, Snapshot());
    }
}
