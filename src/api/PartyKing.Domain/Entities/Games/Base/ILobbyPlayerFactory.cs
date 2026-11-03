namespace PartyKing.Domain.Entities.Games.Base;

/// <summary>Creates the player of a lobby game, so callers never assign a player's properties.</summary>
public interface ILobbyPlayerFactory<out TPlayer>
    where TPlayer : LobbyPlayer
{
    static abstract TPlayer Create(Guid playerId, string name, string? profileId, DateTimeOffset joinedAt);
}
