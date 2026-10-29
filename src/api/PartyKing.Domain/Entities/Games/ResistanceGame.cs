namespace PartyKing.Domain.Entities.Games;

/// <summary>
/// The Resistance game of one session (at most one). For now it is only a lobby: an admin opens it, guests join by
/// name, and the number of players decides the spies and the mission team sizes once the game is started.
/// </summary>
public class ResistanceGame : LobbyGame<ResistancePlayer>
{
    public const int MinPlayers = 5;
    public const int MaxPlayers = 10;
}
