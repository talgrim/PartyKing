using PartyKing.Application.System;

namespace PartyKing.API.Development;

/// <summary>
/// Development only. Gives every action a fresh <see cref="Random"/> from the same seed, so the roles, the first leader,
/// the plot deck and the card taken by "Take Responsibility" are the same whenever the same players are in the game. The
/// e2e suite sets <c>Resistance:Seed</c> for this. Never registered outside Development.
/// </summary>
public sealed class SeededRandomProvider : IRandomProvider
{
    public const string ConfigurationKey = "Resistance:Seed";

    private readonly int _seed;

    public SeededRandomProvider(int seed)
    {
        _seed = seed;
    }

    public Random Create() => new(_seed);

    /// <summary>
    /// Replaces the random source with a seeded one when the host is in Development and <c>Resistance:Seed</c> is set. Like
    /// <c>RateLimiting:Disabled</c> it is honoured in Development only, so a stray setting can never make the deal of a
    /// deployed instance predictable.
    /// </summary>
    public static void AddIfConfigured(IServiceCollection services, IHostEnvironment environment, IConfiguration configuration)
    {
        if (!environment.IsDevelopment() || configuration.GetValue<int?>(ConfigurationKey) is not { } seed)
        {
            return;
        }

        services.AddSingleton<IRandomProvider>(new SeededRandomProvider(seed));
    }
}
