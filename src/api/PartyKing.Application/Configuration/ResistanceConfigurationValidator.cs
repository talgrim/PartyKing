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
            var rules = new ResistanceCountRules(counts.Spies, counts.TeamSizes, counts.RequiredFails);
            var prefix = $"ResistanceSettings:PlayerCounts:{playerCount}";
            errors.AddRange(rules.Validate(playerCount).Select(x => Describe(x, prefix, playerCount)));
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static string Describe(ResistanceRulesFailure failure, string prefix, int playerCount)
    {
        return failure switch
        {
            ResistanceRulesFailure.PlayerCountOutOfRange =>
                $"{prefix}: the player count must be {ResistanceGame.MinPlayers} to {ResistanceGame.MaxPlayers}.",
            ResistanceRulesFailure.SpiesOutOfRange =>
                $"{prefix}:Spies must be at least 1 and fewer than the players.",
            ResistanceRulesFailure.InvalidTeamSizes =>
                $"{prefix}:TeamSizes needs {ResistanceRules.Missions} sizes of 1 to {playerCount}.",
            ResistanceRulesFailure.InvalidRequiredFails =>
                $"{prefix}:RequiredFails needs {ResistanceRules.Missions} numbers, each from 1 to the team size.",
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "No message is defined for this failure."),
        };
    }
}
