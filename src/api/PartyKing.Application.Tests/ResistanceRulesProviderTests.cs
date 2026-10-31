using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Games.Services.Resistance;

namespace PartyKing.Application.Tests;

public class ResistanceRulesProviderTests
{
    private static ResistanceCountConfiguration Counts(int spies, int[] sizes, int[] fails) =>
        new() { Spies = spies, TeamSizes = sizes, RequiredFails = fails };

    [Fact]
    public void WithoutOverrides_ProvidesTheDefaults()
    {
        var provider = new ResistanceRulesProvider(new FakeMonitor(new ResistanceConfiguration()));

        var rules = provider.Current();

        Assert.Equal(3, rules.SpyCount(8));
        Assert.Equal(2, rules.RequiredFails(7, 4));
        Assert.Equal(1, rules.RequiredFails(6, 4));
    }

    [Fact]
    public void Overrides_ReplaceOnlyTheListedPlayerCounts()
    {
        var configuration = new ResistanceConfiguration
        {
            PlayerCounts = { [5] = Counts(1, [2, 2, 2, 2, 2], [1, 1, 1, 1, 2]) },
        };

        var rules = new ResistanceRulesProvider(new FakeMonitor(configuration)).Current();

        Assert.Equal(1, rules.SpyCount(5));
        Assert.Equal(2, rules.RequiredFails(5, 5));
        Assert.Equal(2, rules.SpyCount(6));
    }

    [Fact]
    public void Current_FollowsRuntimeChanges_AndIgnoresInvalidOnes()
    {
        var monitor = new FakeMonitor(new ResistanceConfiguration());
        var provider = new ResistanceRulesProvider(monitor);

        monitor.Value = new ResistanceConfiguration { PlayerCounts = { [5] = Counts(1, [2, 2, 2, 2, 2], [1, 1, 1, 1, 1]) } };
        Assert.Equal(1, provider.Current().SpyCount(5));

        monitor.Invalid = true;
        Assert.Equal(1, provider.Current().SpyCount(5));
    }

    [Fact]
    public void Current_ReturnsACopyEachTime()
    {
        var provider = new ResistanceRulesProvider(new FakeMonitor(new ResistanceConfiguration()));

        provider.Current().PlayerCounts.Clear();

        Assert.Equal(6, provider.Current().PlayerCounts.Count);
    }

    [Theory]
    [InlineData(4, 1, 5, 1)]
    [InlineData(5, 0, 5, 1)]
    [InlineData(5, 5, 5, 1)]
    [InlineData(5, 2, 4, 1)]
    [InlineData(5, 2, 5, 6)]
    [InlineData(5, 2, 5, 0)]
    public void Validator_RejectsBrokenRules(int playerCount, int spies, int sizeCount, int fails)
    {
        var configuration = new ResistanceConfiguration
        {
            PlayerCounts =
            {
                [playerCount] = Counts(
                    spies,
                    Enumerable.Repeat(playerCount == 5 ? 3 : 2, sizeCount == 5 ? 5 : sizeCount).ToArray(),
                    Enumerable.Repeat(fails, 5).ToArray()),
            },
        };

        Assert.True(new ResistanceConfigurationValidator().Validate(null, configuration).Failed);
    }

    [Fact]
    public void Validator_AcceptsGoodRules()
    {
        var configuration = new ResistanceConfiguration
        {
            PlayerCounts = { [7] = Counts(3, [2, 3, 3, 4, 4], [1, 1, 1, 2, 1]) },
        };

        Assert.True(new ResistanceConfigurationValidator().Validate(null, configuration).Succeeded);
    }

    private sealed class FakeMonitor : IOptionsMonitor<ResistanceConfiguration>
    {
        public FakeMonitor(ResistanceConfiguration value) => Value = value;

        public ResistanceConfiguration Value { get; set; }

        public bool Invalid { get; set; }

        public ResistanceConfiguration CurrentValue => Invalid
            ? throw new OptionsValidationException("", typeof(ResistanceConfiguration), ["bad"])
            : Value;

        public ResistanceConfiguration Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<ResistanceConfiguration, string?> listener) => null;
    }
}
