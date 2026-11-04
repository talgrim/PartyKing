using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Domain.Tests;

/// <summary>The validation of the rules of one player count, with no configuration and no database.</summary>
public class ResistanceCountRulesTests
{
    [Fact]
    public void Validate_AcceptsEveryDefaultCount()
    {
        foreach (var (playerCount, counts) in ResistanceRules.Default().PlayerCounts)
        {
            Assert.Empty(counts.Validate(playerCount));
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(11)]
    public void Validate_OfAPlayerCountOutsideTheRange_ReportsOnlyThat(int playerCount)
    {
        var counts = new ResistanceCountRules(0, [], []);

        Assert.Equal([ResistanceRulesFailure.PlayerCountOutOfRange], counts.Validate(playerCount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Validate_RefusesSpiesThatAreNoneOrAsManyAsThePlayers(int spies)
    {
        var counts = new ResistanceCountRules(spies, [2, 3, 2, 3, 3], [1, 1, 1, 1, 1]);

        Assert.Equal([ResistanceRulesFailure.SpiesOutOfRange], counts.Validate(5));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void Validate_RefusesTeamSizesThatAreNotFive(int sizeCount)
    {
        var counts = new ResistanceCountRules(2, Enumerable.Repeat(2, sizeCount).ToArray(), [1, 1, 1, 1, 1]);

        Assert.Equal([ResistanceRulesFailure.InvalidTeamSizes], counts.Validate(5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Validate_RefusesATeamSizeBelowOneOrAboveThePlayers(int size)
    {
        var counts = new ResistanceCountRules(2, [2, 3, size, 3, 3], [1, 1, 1, 1, 1]);

        Assert.Equal([ResistanceRulesFailure.InvalidTeamSizes], counts.Validate(5));
    }

    [Fact]
    public void Validate_DoesNotCheckTheRequiredFailsAgainstBrokenTeamSizes()
    {
        var counts = new ResistanceCountRules(2, [2, 3, 0, 3, 3], [1, 1, 9, 1, 1]);

        Assert.Equal([ResistanceRulesFailure.InvalidTeamSizes], counts.Validate(5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Validate_RefusesRequiredFailsBelowOneOrAboveTheTeamSize(int fails)
    {
        // Mission 1 sends 2 players
        var counts = new ResistanceCountRules(2, [2, 3, 2, 3, 3], [fails, 1, 1, 1, 1]);

        Assert.Equal([ResistanceRulesFailure.InvalidRequiredFails], counts.Validate(5));
    }

    [Fact]
    public void Validate_RefusesRequiredFailsThatAreNotFive()
    {
        var counts = new ResistanceCountRules(2, [2, 3, 2, 3, 3], [1, 1, 1, 1]);

        Assert.Equal([ResistanceRulesFailure.InvalidRequiredFails], counts.Validate(5));
    }

    [Fact]
    public void Validate_ReportsEveryIndependentFailure()
    {
        var counts = new ResistanceCountRules(0, [2, 3, 2, 3, 3], [0, 1, 1, 1, 1]);

        Assert.Equal(
            [ResistanceRulesFailure.SpiesOutOfRange, ResistanceRulesFailure.InvalidRequiredFails],
            counts.Validate(5));
    }
}
