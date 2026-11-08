using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Domain.Tests;

/// <summary>The Blind Spies variant of <see cref="ResistanceGame"/> and who knows whom, with no database.</summary>
public class ResistanceGameBlindSpiesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 20, 0, 0, TimeSpan.Zero);

    /// <summary>Seven players: seats 0-2 are the spies, seats 3-6 the resistance.</summary>
    private static ResistanceGame SevenPlayerGame(bool blindSpies)
    {
        var rules = ResistanceRules.Default();
        rules.BlindSpiesEnabled = blindSpies;
        var game = new ResistanceGame { SessionId = Guid.NewGuid(), IsOpen = true, StartedAt = Now, Rules = rules };
        for (var i = 0; i < 7; i++)
        {
            game.Players.Add(new ResistancePlayer
            {
                PlayerId = Guid.NewGuid(),
                Order = i,
                Role = i < 3 ? ResistanceRole.Spy : ResistanceRole.Resistance,
            });
        }

        return game;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetBlindSpies_BeforeTheGameStarts_SwitchesTheVariantOnANewRulesObject(bool open)
    {
        var rules = ResistanceRules.Default();
        rules.PlotCardsEnabled = true;
        rules.InquisitorEnabled = true;
        var game = new ResistanceGame { IsOpen = open, Rules = rules };

        var outcome = game.SetBlindSpies(true);

        Assert.Null(outcome.Failure);
        Assert.True(game.Rules.BlindSpiesEnabled);
        Assert.True(game.Rules.PlotCardsEnabled);
        Assert.True(game.Rules.InquisitorEnabled);
        Assert.NotSame(rules, game.Rules);
        Assert.False(rules.BlindSpiesEnabled);
    }

    [Fact]
    public void SetBlindSpies_CanSwitchTheVariantBackOff()
    {
        var game = new ResistanceGame { Rules = ResistanceRules.Default() };
        game.SetBlindSpies(true);

        var outcome = game.SetBlindSpies(false);

        Assert.Null(outcome.Failure);
        Assert.False(game.Rules.BlindSpiesEnabled);
    }

    [Fact]
    public void SetBlindSpies_OnceTheGameStarted_IsRefusedAndChangesNothing()
    {
        var rules = ResistanceRules.Default();
        var game = new ResistanceGame { StartedAt = Now, Rules = rules };

        var outcome = game.SetBlindSpies(true);

        Assert.Equal(ResistanceFailure.BlindSpiesLocked, outcome.Failure);
        Assert.Same(rules, game.Rules);
        Assert.False(game.Rules.BlindSpiesEnabled);
    }

    [Fact]
    public void TheOtherSwitches_KeepTheBlindSpiesFlag()
    {
        var rules = ResistanceRules.Default();
        rules.BlindSpiesEnabled = true;
        var game = new ResistanceGame { Rules = rules };

        game.SetPlotCards(true);
        game.SetInquisitor(true);
        Assert.True(game.Rules.BlindSpiesEnabled);

        game.RefreshRules(ResistanceRules.Default());
        Assert.True(game.Rules.BlindSpiesEnabled);
        Assert.True(game.Rules.PlotCardsEnabled);
        Assert.True(game.Rules.InquisitorEnabled);
    }

    [Fact]
    public void Clone_CopiesTheFlag_AndAnOldRulesObjectHasItOff()
    {
        Assert.False(ResistanceRules.Default().BlindSpiesEnabled);

        var rules = ResistanceRules.Default();
        rules.BlindSpiesEnabled = true;

        Assert.True(rules.Clone().BlindSpiesEnabled);
        Assert.False(ResistanceRules.Default().Clone().BlindSpiesEnabled);
    }

    /// <summary>The whole viewer x target table: a leak here is a security bug, so every pair is pinned.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KnowsSpy_EveryViewerAndTarget(bool blindSpies)
    {
        var game = SevenPlayerGame(blindSpies);

        foreach (var viewer in game.Players)
        {
            foreach (var target in game.Players)
            {
                var expected = !blindSpies
                    && viewer.Role == ResistanceRole.Spy
                    && target.Role == ResistanceRole.Spy
                    && viewer.PlayerId != target.PlayerId;

                Assert.Equal(expected, ResistanceKnowledge.KnowsSpy(game, viewer, target));
            }
        }
    }
}
