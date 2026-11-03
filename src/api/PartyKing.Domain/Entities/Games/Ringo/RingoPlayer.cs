using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Domain.Entities.Games.Ringo;

/// <summary>A person who joined a Ringo game by name. The turn order is <see cref="LobbyPlayer.Order"/>.</summary>
public class RingoPlayer : LobbyPlayer, ILobbyPlayerFactory<RingoPlayer>
{
    internal RingoPlayer()
    {
    }

    public static RingoPlayer Create(Guid playerId, string name, string? profileId, DateTimeOffset joinedAt)
    {
        var player = new RingoPlayer();
        player.Initialize(playerId, name, profileId, joinedAt);

        return player;
    }
}
