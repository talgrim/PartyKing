using Microsoft.Extensions.Options;
using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Application.Configuration;

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
