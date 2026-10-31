using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Infrastructure.Repositories;

public interface IRingoGamesRepository : ILobbyGamesRepository<RingoGame, RingoPlayer>
{
    /// <summary>
    /// Records the flip of a card and makes it the pending one. A card that is already flipped changes nothing, and
    /// while another card is pending nothing is flipped (see <see cref="RingoFlipStatus"/>). With players, the flip is
    /// recorded under the name of the player whose turn it is, and <paramref name="turnOf"/> (null for a manager, who
    /// may flip out of turn) must be that player; without players <paramref name="flippedBy"/> is recorded.
    /// </summary>
    Task<RingoFlipResult> FlipAsync(
        Guid sessionId,
        int cardId,
        string flippedBy,
        Guid? turnOf,
        DateTimeOffset flippedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Clears the pending card (idempotent) and, when there was one, passes the turn to the next player.
    /// <paramref name="turnOf"/> (null for a manager) must be the player whose turn it is.
    /// </summary>
    Task<RingoContinueResult> ContinueAsync(Guid sessionId, Guid? turnOf, CancellationToken cancellationToken);
}
