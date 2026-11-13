using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Domain.Tests;

/// <summary>
/// The order the modules answer <see cref="ResistanceKnowledge.KnowsSpy"/> in: the ones that decide outright (the Blind
/// Spies variant, then the Commander) before the ones that hide a player. The API never lets the Assassin module be
/// played with the other three, so the games here are built by hand to make the order show.
/// </summary>
public class ResistanceKnowledgeOrderTests
{
    private static ResistanceGame Table(
        Action<ResistanceRules> configure,
        params (ResistanceRole Role, ResistanceCharacter Character)[] seats)
    {
        var rules = ResistanceRules.Default();
        configure(rules);
        var game = new ResistanceGame
        {
            SessionId = Guid.NewGuid(),
            IsOpen = true,
            StartedAt = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero),
            Rules = rules,
        };
        for (var i = 0; i < seats.Length; i++)
        {
            game.Players.Add(new ResistancePlayer
            {
                PlayerId = Guid.NewGuid(),
                Order = i,
                Role = seats[i].Role,
                Character = seats[i].Character,
            });
        }

        return game;
    }

    private static bool Knows(ResistanceGame game, int viewer, int target)
    {
        return ResistanceKnowledge.KnowsSpy(game, game.Players[viewer], game.Players[target]);
    }

    [Fact]
    public void TheModulesThatChangeWhoKnowsWhom_AreAskedInTheOrderOfTheRegistry()
    {
        Assert.Equal(
            [
                ResistanceModule.BlindSpies,
                ResistanceModule.Reverser,
                ResistanceModule.Defector,
                ResistanceModule.Assassin,
            ],
            ResistanceModules.Knowledge.Select(x => x.Module));
    }

    [Fact]
    public void TheBlindSpiesVariant_DecidesBeforeTheCommander()
    {
        var game = Table(
            x => x.BlindSpiesEnabled = true,
            (ResistanceRole.Resistance, ResistanceCharacter.Commander),
            (ResistanceRole.Spy, ResistanceCharacter.Standard),
            (ResistanceRole.Spy, ResistanceCharacter.Standard));

        Assert.False(Knows(game, 0, 1));
        Assert.False(Knows(game, 1, 2));
    }

    [Fact]
    public void TheCommander_DecidesBeforeTheModulesThatHide()
    {
        // A Blind Spy, a hidden Reverser spy and a Defector among the spies, and Deep Cover, who hides from the Commander only
        var game = Table(
            x =>
            {
                x.ReverserEnabled = true;
                x.ReverserSpyKnowsSpies = false;
            },
            (ResistanceRole.Resistance, ResistanceCharacter.Commander),
            (ResistanceRole.Spy, ResistanceCharacter.BlindSpy),
            (ResistanceRole.Spy, ResistanceCharacter.Reverser),
            (ResistanceRole.Spy, ResistanceCharacter.Defector),
            (ResistanceRole.Spy, ResistanceCharacter.DeepCover));

        Assert.True(Knows(game, 0, 1));
        Assert.True(Knows(game, 0, 2));
        Assert.True(Knows(game, 0, 3));
        Assert.False(Knows(game, 0, 4));
    }

    [Fact]
    public void ThoseWhoHide_HideFromEveryoneButTheCommander()
    {
        var game = Table(
            x =>
            {
                x.ReverserEnabled = true;
                x.ReverserSpyKnowsSpies = false;
            },
            (ResistanceRole.Resistance, ResistanceCharacter.Commander),
            (ResistanceRole.Spy, ResistanceCharacter.Standard),
            (ResistanceRole.Spy, ResistanceCharacter.BlindSpy),
            (ResistanceRole.Spy, ResistanceCharacter.Reverser),
            (ResistanceRole.Spy, ResistanceCharacter.Standard));

        Assert.True(Knows(game, 1, 4));

        foreach (var hidden in new[] { 2, 3 })
        {
            Assert.False(Knows(game, 1, hidden));
            Assert.False(Knows(game, hidden, 1));
            Assert.False(Knows(game, 4, hidden));
        }
    }

    [Fact]
    public void TheCommanderSeesSpies_AndTheModulesThatAddNobodyElseDoNot()
    {
        var game = Table(
            _ => { },
            (ResistanceRole.Resistance, ResistanceCharacter.Commander),
            (ResistanceRole.Resistance, ResistanceCharacter.Standard),
            (ResistanceRole.Spy, ResistanceCharacter.Standard));

        Assert.True(ResistanceKnowledge.SeesSpies(game.Players[0]));
        Assert.False(ResistanceKnowledge.SeesSpies(game.Players[1]));
        Assert.True(ResistanceKnowledge.SeesSpies(game.Players[2]));
    }
}
