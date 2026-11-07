using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Domain.Tests;

/// <summary>Which rule modules of <see cref="ResistanceGame"/> combine, pair by pair, whichever is switched on first.</summary>
public class ResistanceGameModuleCombinationTests
{
    private static readonly Dictionary<string, Func<ResistanceGame, bool, ResistanceOutcome>> Switches = new()
    {
        ["PlotCards"] = (game, enabled) => game.SetPlotCards(enabled),
        ["Inquisitor"] = (game, enabled) => game.SetInquisitor(enabled),
        ["BlindSpies"] = (game, enabled) => game.SetBlindSpies(enabled),
        ["Reverser"] = (game, enabled) => game.SetReverser(enabled),
        ["Defector"] = (game, enabled) => game.SetDefector(enabled),
        ["Assassin"] = (game, enabled) => game.SetAssassin(enabled),
    };

    private static readonly string[] AgainstTheAssassin = ["BlindSpies", "Reverser", "Defector"];

    public static TheoryData<string, string> EveryOrderedPair()
    {
        var pairs = new TheoryData<string, string>();

        foreach (var first in Switches.Keys)
        {
            foreach (var second in Switches.Keys.Where(x => x != first))
            {
                pairs.Add(first, second);
            }
        }

        return pairs;
    }

    private static bool Conflict(string first, string second) =>
        (first == "Assassin" && AgainstTheAssassin.Contains(second))
        || (second == "Assassin" && AgainstTheAssassin.Contains(first));

    [Theory]
    [MemberData(nameof(EveryOrderedPair))]
    public void SwitchingASecondModuleOn_IsRefusedOnlyAgainstTheAssassinConflict(string first, string second)
    {
        var game = new ResistanceGame { Rules = ResistanceRules.Default() };
        Assert.Null(Switches[first](game, true).Failure);
        var before = game.Rules;

        var outcome = Switches[second](game, true);

        if (Conflict(first, second))
        {
            Assert.Equal(ResistanceFailure.AssassinConflict, outcome.Failure);
            Assert.False(outcome.Changed);
            Assert.Same(before, game.Rules);
        }
        else
        {
            Assert.Null(outcome.Failure);
            Assert.True(outcome.Changed);
            Assert.NotSame(before, game.Rules);
        }
    }

    [Theory]
    [MemberData(nameof(EveryOrderedPair))]
    public void SwitchingAModuleOff_IsAlwaysAllowed_WhateverIsOn(string first, string second)
    {
        var game = new ResistanceGame { Rules = ResistanceRules.Default() };
        Assert.Null(Switches[first](game, true).Failure);

        var outcome = Switches[second](game, false);

        Assert.Null(outcome.Failure);
        Assert.True(outcome.Changed);
    }
}
