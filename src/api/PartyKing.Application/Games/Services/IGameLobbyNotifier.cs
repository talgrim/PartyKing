namespace PartyKing.Application.Games.Services;

/// <summary>Tells the session's clients that a game's lobby changed.</summary>
public interface IGameLobbyNotifier
{
    /// <summary>An admin opened the game; guests are asked whether to join.</summary>
    Task OpenedAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Anything about the game changed; clients fetch it again.</summary>
    Task ChangedAsync(Guid sessionId, CancellationToken cancellationToken);
}
