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
