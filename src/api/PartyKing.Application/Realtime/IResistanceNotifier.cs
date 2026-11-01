using PartyKing.Application.Games.Services.Base;

namespace PartyKing.Application.Realtime;

/// <summary>
/// Pushes Resistance changes to a session: <c>OpenedAsync</c> when an admin opens the game to guests,
/// <c>ChangedAsync</c> when anything about the game changed.
/// </summary>
public interface IResistanceNotifier : IGameLobbyNotifier;
