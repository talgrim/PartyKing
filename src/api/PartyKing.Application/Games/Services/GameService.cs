using ErrorOr;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Games;

namespace PartyKing.Application.Games.Services;

public interface IGameService
{
    /// <summary>The games available in the session. Host or admin only: Forbidden for anyone else, NotFound for an unknown session.</summary>
    Task<ErrorOr<IReadOnlyList<GameDto>>> ListAsync(
        Guid sessionId,
        string? profileId,
        bool isAdmin,
        CancellationToken cancellationToken);
}

internal class GameService : IGameService
{
    // Placeholder list until games are stored and started per session
    private static readonly IReadOnlyList<GameDto> Games =
    [
        new("ringo", "Ringo", "A party game played with the whole room."),
        new("never-have-i-ever", "Never have I ever", "Guests confess what they have never done."),
    ];

    private readonly ISessionService _sessionService;

    public GameService(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    public async Task<ErrorOr<IReadOnlyList<GameDto>>> ListAsync(
        Guid sessionId,
        string? profileId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        if (await _sessionService.AuthorizeManagerAsync(sessionId, profileId, isAdmin, cancellationToken)
            is { IsError: true } denied)
        {
            return denied.Errors;
        }

        return ErrorOrFactory.From(Games);
    }
}
