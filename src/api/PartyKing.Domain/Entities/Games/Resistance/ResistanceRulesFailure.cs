namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>What can be wrong with the <see cref="ResistanceCountRules"/> of one player count.</summary>
public enum ResistanceRulesFailure
{
    /// <summary>The player count is not 5 to 10.</summary>
    PlayerCountOutOfRange,

    /// <summary>There is no spy, or as many spies as players.</summary>
    SpiesOutOfRange,

    /// <summary>There are not five team sizes, or one of them is below 1 or above the player count.</summary>
    InvalidTeamSizes,

    /// <summary>There are not five required-fail numbers, or one is below 1 or above its mission's team size.</summary>
    InvalidRequiredFails,
}
