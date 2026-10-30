using Microsoft.Extensions.Options;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Configuration;

/// <summary>
/// Overrides of the Resistance rules, bound from <c>ResistanceSettings</c> and re-read at runtime (<see cref="IOptionsMonitor{T}"/>).
/// Only the player counts listed here are overridden, each one completely; the others keep <see cref="ResistanceRules.Default"/>.
/// A game copies the rules when its lobby is created or refreshed, so a change never alters a game in progress.
/// </summary>
public class ResistanceConfiguration
{
    internal const string SectionName = "ResistanceSettings";

    public Dictionary<int, ResistanceCountConfiguration> PlayerCounts { get; set; } = new();
}

public class ResistanceCountConfiguration
{
    /// <summary>How many of the players are spies.</summary>
    public int Spies { get; set; }

    /// <summary>The team size of missions 1 to 5.</summary>
    public int[] TeamSizes { get; set; } = [];

    /// <summary>The Fail cards that make missions 1 to 5 fail.</summary>
    public int[] RequiredFails { get; set; } = [];
}

internal class ResistanceConfigurationValidator : IValidateOptions<ResistanceConfiguration>
{
    public ValidateOptionsResult Validate(string? name, ResistanceConfiguration options)
    {
        var errors = new List<string>();

        foreach (var (playerCount, counts) in options.PlayerCounts)
        {
            var prefix = $"ResistanceSettings:PlayerCounts:{playerCount}";

            if (playerCount is < ResistanceGame.MinPlayers or > ResistanceGame.MaxPlayers)
            {
                errors.Add($"{prefix}: the player count must be {ResistanceGame.MinPlayers} to {ResistanceGame.MaxPlayers}.");
                continue;
            }

            if (counts.Spies < 1 || counts.Spies >= playerCount)
            {
                errors.Add($"{prefix}:Spies must be at least 1 and fewer than the players.");
            }

            if (counts.TeamSizes.Length != ResistanceRules.Missions
                || counts.TeamSizes.Any(x => x < 1 || x > playerCount))
            {
                errors.Add($"{prefix}:TeamSizes needs {ResistanceRules.Missions} sizes of 1 to {playerCount}.");
                continue;
            }

            if (counts.RequiredFails.Length != ResistanceRules.Missions
                || counts.RequiredFails.Zip(counts.TeamSizes).Any(x => x.First < 1 || x.First > x.Second))
            {
                errors.Add($"{prefix}:RequiredFails needs {ResistanceRules.Missions} numbers, each from 1 to the team size.");
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
