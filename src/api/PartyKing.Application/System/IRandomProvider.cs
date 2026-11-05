namespace PartyKing.Application.System;

/// <summary>Source of the randomness of a game action (dealing roles, shuffling a deck, drawing a card).</summary>
public interface IRandomProvider
{
    /// <summary>The <see cref="Random"/> to use for one action.</summary>
    Random Create();
}

internal class SharedRandomProvider : IRandomProvider
{
    public Random Create() => Random.Shared;
}
