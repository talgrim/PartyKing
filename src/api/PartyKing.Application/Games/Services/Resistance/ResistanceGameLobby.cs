using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Domain.Repositories;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.System;
using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Application.Games.Services.Resistance;

/// <summary>
/// The lobby of the Resistance game: 5 to 10 players, its pushes and the "Resistance" error names. In the demo the
/// host opens it, as there is no admin.
/// </summary>
internal sealed class ResistanceGameLobby : GameLobby<ResistanceGame, ResistancePlayer>
{
    public ResistanceGameLobby(
        ISessionAuthorizer sessionAuthorizer,
        ISessionsRepository sessions,
        IResistanceGamesRepository games,
        IDateTimeProvider clock,
        IOptions<DemoConfiguration>? demo = null)
        : base(
            sessionAuthorizer,
            sessions,
            games,
            clock,
            new GameLobbyOptions(ResistanceErrors.GameName, ResistanceGame.MaxPlayers),
            demo?.Value.Enabled == true)
    {
    }
}
