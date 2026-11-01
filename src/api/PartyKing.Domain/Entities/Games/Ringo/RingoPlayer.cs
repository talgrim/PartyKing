using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Domain.Entities.Games.Ringo;

/// <summary>A person who joined a Ringo game by name. The turn order is <see cref="LobbyPlayer.Order"/>.</summary>
public class RingoPlayer : LobbyPlayer;
