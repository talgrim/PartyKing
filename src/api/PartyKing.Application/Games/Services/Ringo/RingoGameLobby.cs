using PartyKing.Application.Sessions.Services;
using PartyKing.Application.System;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Games.Services.Ringo;

/// <summary>The lobby of the Ringo game: any number of players, its pushes and the "Ringo" error names.</summary>
internal sealed class RingoGameLobby : GameLobby<RingoGame, RingoPlayer>
{
    public RingoGameLobby(
        ISessionAuthorizer sessionAuthorizer,
        ISessionsRepository sessions,
        IRingoGamesRepository games,
        IDateTimeProvider clock,
        RingoLobbyNotifier notifier)
        : base(sessionAuthorizer, sessions, games, clock, notifier, new GameLobbyOptions("Ringo"))
    {
    }
}
