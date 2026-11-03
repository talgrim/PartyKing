using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Application.Tests;

/// <summary>The rules switches of <see cref="ResistanceGame"/>, with no database.</summary>
public class ResistanceGameRulesTests
{
    private static ResistanceRules NewRules(bool plotCards = false)
    {
        var rules = ResistanceRules.Default();
        rules.PlotCardsEnabled = plotCards;

        return rules;
    }

    [Fact]
    public void RefreshRules_InAClosedLobby_ReplacesTheRulesAndKeepsThePlotCardSwitch()
    {
        var game = new ResistanceGame { Rules = NewRules(plotCards: true) };
        var fresh = NewRules(plotCards: false);

        var outcome = game.RefreshRules(fresh);

        Assert.Null(outcome.Failure);
        Assert.True(outcome.Changed);
        Assert.Same(game, outcome.Game);
        // The game keeps a copy: the caller's object is neither shared nor changed
        Assert.NotSame(fresh, game.Rules);
        Assert.False(fresh.PlotCardsEnabled);
        Assert.True(game.Rules.PlotCardsEnabled);
        Assert.Equal(fresh.PlayerCounts.Keys, game.Rules.PlayerCounts.Keys);
    }

    [Fact]
    public void RefreshRules_OnceTheGameIsOpen_IsRefusedAndChangesNothing()
    {
        var rules = NewRules();
        var game = new ResistanceGame { IsOpen = true, Rules = rules };

        var outcome = game.RefreshRules(NewRules());

        Assert.Equal(ResistanceFailure.RulesLocked, outcome.Failure);
        Assert.False(outcome.Changed);
        Assert.Same(rules, game.Rules);
    }

    [Fact]
    public void RefreshRules_OnceTheGameStarted_IsRefused()
    {
        var game = new ResistanceGame { StartedAt = DateTimeOffset.UtcNow };

        Assert.Equal(ResistanceFailure.RulesLocked, game.RefreshRules(NewRules()).Failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetPlotCards_AnyTimeBeforeTheStart_SwitchesThemWithANewRulesInstance(bool open)
    {
        var rules = NewRules();
        var game = new ResistanceGame { IsOpen = open, Rules = rules };

        var outcome = game.SetPlotCards(true);

        Assert.Null(outcome.Failure);
        Assert.True(outcome.Changed);
        Assert.True(game.Rules.PlotCardsEnabled);
        Assert.NotSame(rules, game.Rules);
        Assert.False(rules.PlotCardsEnabled);
        Assert.Equal(rules.PlayerCounts.Keys, game.Rules.PlayerCounts.Keys);
        Assert.Equal(rules.PlotDeck, game.Rules.PlotDeck);
        Assert.NotSame(rules.PlotDeck, game.Rules.PlotDeck);
    }

    [Fact]
    public void SetPlotCards_OnceTheGameStarted_IsRefusedAndChangesNothing()
    {
        var rules = NewRules();
        var game = new ResistanceGame { StartedAt = DateTimeOffset.UtcNow, Rules = rules };

        var outcome = game.SetPlotCards(true);

        Assert.Equal(ResistanceFailure.PlotCardsLocked, outcome.Failure);
        Assert.False(outcome.Changed);
        Assert.Same(rules, game.Rules);
        Assert.False(game.Rules.PlotCardsEnabled);
    }
}
