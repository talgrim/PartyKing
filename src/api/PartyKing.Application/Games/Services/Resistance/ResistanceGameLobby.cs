using PartyKing.Application.Sessions.Services;
using PartyKing.Application.System;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Games.Services.Resistance;

/// <summary>The lobby of the Resistance game: 5 to 10 players, its pushes and the "Resistance" error names.</summary>
internal sealed class ResistanceGameLobby : GameLobby<ResistanceGame, ResistancePlayer>
{
    public ResistanceGameLobby(
        ISessionAuthorizer sessionAuthorizer,
        ISessionsRepository sessions,
        IResistanceGamesRepository games,
        IDateTimeProvider clock,
        ResistanceLobbyNotifier notifier)
        : base(
            sessionAuthorizer,
            sessions,
            games,
            clock,
            notifier,
            new GameLobbyOptions(ResistanceErrors.GameName, ResistanceGame.MaxPlayers))
    {
    }
}
