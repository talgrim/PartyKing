using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Games.Services;

public interface IResistanceRulesProvider
{
    /// <summary>
    /// A fresh copy of the rules from the current server configuration. An invalid configuration edited at runtime
    /// is ignored and the last valid rules stay in force.
    /// </summary>
    ResistanceRules Current();
}

internal class ResistanceRulesProvider : IResistanceRulesProvider
{
    private readonly IOptionsMonitor<ResistanceConfiguration> _options;
    private ResistanceRules _lastValid;

    public ResistanceRulesProvider(IOptionsMonitor<ResistanceConfiguration> options)
    {
        _options = options;
        _lastValid = Build(options.CurrentValue);
    }

    public ResistanceRules Current()
    {
        try
        {
            _lastValid = Build(_options.CurrentValue);
        }
        catch (OptionsValidationException)
        {
        }

        return Copy(_lastValid);
    }

    private static ResistanceRules Build(ResistanceConfiguration configuration)
    {
        var rules = ResistanceRules.Default();
        foreach (var (playerCount, counts) in configuration.PlayerCounts)
        {
            rules.PlayerCounts[playerCount] = new ResistanceCountRules(
                counts.Spies, [.. counts.TeamSizes], [.. counts.RequiredFails]);
        }

        return rules;
    }

    private static ResistanceRules Copy(ResistanceRules rules) => new()
    {
        PlayerCounts = rules.PlayerCounts.ToDictionary(
            x => x.Key, x => new ResistanceCountRules(x.Value.Spies, [.. x.Value.TeamSizes], [.. x.Value.RequiredFails])),
        PlotCardsEnabled = rules.PlotCardsEnabled,
        PlotDeck = new Dictionary<ResistancePlotCardType, int>(rules.PlotDeck),
    };
}
