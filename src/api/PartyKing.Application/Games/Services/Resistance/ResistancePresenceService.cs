using ErrorOr;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Application.Realtime;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Application.Games.Services.Resistance;

/// <summary>Who is connected to the Resistance game: the snapshot a TV view starts from and refetches after a reconnect.</summary>
public interface IResistancePresenceService
{
    /// <summary>Host or admin: the players that are online now. Forbidden for anyone else, NotFound for an unknown session.</summary>
    Task<ErrorOr<ResistancePresenceDto>> GetAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken);
}

internal sealed class ResistancePresenceService : IResistancePresenceService
{
    private readonly ResistanceGameLobby _lobby;
    private readonly IGamePresence _presence;

    public ResistancePresenceService(ResistanceGameLobby lobby, IGamePresence presence)
    {
        _lobby = lobby;
        _presence = presence;
    }

    public async Task<ErrorOr<ResistancePresenceDto>> GetAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        if (await _lobby.AuthorizeAsync(sessionId, caller, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        return new ResistancePresenceDto(_presence.OnlinePlayers(GameKind.Resistance, sessionId));
    }
}
