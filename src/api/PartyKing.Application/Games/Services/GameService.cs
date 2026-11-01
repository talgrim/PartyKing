using PartyKing.Application.Sessions.Helpers;
using ErrorOr;
using PartyKing.Application.Localization;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Sessions;
using PartyKing.Contract.V1.Games;

namespace PartyKing.Application.Games.Services;

public interface IGameService
{
    /// <summary>
    /// The games available in the session, named and described in <paramref name="language"/>. Host or admin only:
    /// Forbidden for anyone else, NotFound for an unknown session.
    /// </summary>
    Task<ErrorOr<IReadOnlyList<GameDto>>> ListAsync(
        Guid sessionId,
        SessionCaller caller,
        UiLanguage language,
        CancellationToken cancellationToken);
}

internal class GameService : IGameService
{
    // Placeholder lists until games are stored and started per session. Ringo and Resistance keep their names.
    private static readonly IReadOnlyList<GameDto> EnglishGames =
    [
        new("resistance", "Resistance", "A social deduction game for 5-10 players: the Resistance against the Spies."),
        new("ringo", "Ringo", "A party card game played with the whole room."),
        new("never-have-i-ever", "Never have I ever", "Guests confess what they have never done."),
    ];

    private static readonly IReadOnlyList<GameDto> PolishGames =
    [
        new("resistance", "Resistance", "Gra dedukcji społecznej dla 5-10 osób: Ruch Oporu kontra Szpiedzy."),
        new("ringo", "Ringo", "Imprezowa gra karciana dla całego pokoju."),
        new("never-have-i-ever", "Nigdy w życiu", "Goście wyznają, czego nigdy nie zrobili."),
    ];

    private readonly ISessionAuthorizer _sessionAuthorizer;

    public GameService(ISessionAuthorizer sessionAuthorizer)
    {
        _sessionAuthorizer = sessionAuthorizer;
    }

    public Task<ErrorOr<IReadOnlyList<GameDto>>> ListAsync(
        Guid sessionId,
        SessionCaller caller,
        UiLanguage language,
        CancellationToken cancellationToken)
    {
        return _sessionAuthorizer.RunAsManagerAsync(
            sessionId,
            caller,
            () => ListCoreAsync(language),
            cancellationToken);
    }

    private static Task<ErrorOr<IReadOnlyList<GameDto>>> ListCoreAsync(UiLanguage language)
    {
        return Task.FromResult(ErrorOrFactory.From(language == UiLanguage.Pl ? PolishGames : EnglishGames));
    }
}
