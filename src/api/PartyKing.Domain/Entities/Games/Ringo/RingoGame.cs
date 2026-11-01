using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Domain.Entities.Games.Ringo;

/// <summary>The Ringo game of one session (at most one): the seed of the card layout and the flipped cards in flip order.</summary>
public class RingoGame : LobbyGame<RingoPlayer>
{
    public int Seed { get; set; }

    /// <summary>The last flipped card until the host or an admin continues; null when none awaits that.</summary>
    public int? PendingCardId { get; set; }

    /// <summary>The player whose turn it is; null while nobody has joined.</summary>
    public Guid? CurrentPlayerId { get; set; }

    public List<RingoFlip> Flips { get; set; } = [];

    protected override void OnPlayerJoined(RingoPlayer player)
    {
        CurrentPlayerId ??= player.PlayerId;
    }

    protected override void OnPlayerKicking(RingoPlayer kicked)
    {
        var wasCurrent = RingoTurns.Current(this)?.PlayerId == kicked.PlayerId;
        CurrentPlayerId = RingoTurns.AfterRemoving(this, kicked, wasCurrent)?.PlayerId;
    }

    /// <summary>The fourth king ends the game: nothing can be flipped any more.</summary>
    public bool IsOver => Flips.Count(x => x.CardId % 13 == KingRankIndex) >= KingsToEnd;

    /// <summary>
    /// Records the flip of a card and makes it the pending one. With players the flip is recorded under the name of the
    /// player whose turn it is, and <paramref name="turnOf"/> (null for a manager, who may flip out of turn) must be
    /// that player; without players <paramref name="flippedBy"/> is recorded. A card that is flipped already changes
    /// nothing, and is not a failure.
    /// </summary>
    public RingoOutcome Flip(int cardId, string flippedBy, Guid? turnOf, DateTimeOffset flippedAt)
    {
        var current = RingoTurns.Current(this);
        if (turnOf is not null && turnOf != current?.PlayerId)
        {
            return RingoOutcome.Failed(RingoFailure.NotYourTurn, this);
        }

        if (Flips.Any(x => x.CardId == cardId))
        {
            return RingoOutcome.Unchanged(this);
        }

        if (IsOver)
        {
            return RingoOutcome.Failed(RingoFailure.GameOver, this);
        }

        if (PendingCardId is not null)
        {
            return RingoOutcome.Failed(RingoFailure.CardPending, this);
        }

        Flips.Add(new RingoFlip
        {
            SessionId = SessionId,
            Order = Flips.Count,
            CardId = cardId,
            FlippedAt = flippedAt,
            FlippedBy = current?.DisplayName ?? flippedBy,
        });
        PendingCardId = cardId;
        CurrentPlayerId = current?.PlayerId;

        return RingoOutcome.Done(this);
    }

    /// <summary>
    /// Clears the pending card and, when there was one, passes the turn to the next player. <paramref name="turnOf"/>
    /// (null for a manager) must be the player whose turn it is. Without a pending card nothing changes.
    /// </summary>
    public RingoOutcome Continue(Guid? turnOf)
    {
        if (turnOf is not null && turnOf != RingoTurns.Current(this)?.PlayerId)
        {
            return RingoOutcome.Failed(RingoFailure.NotYourTurn, this);
        }

        if (PendingCardId is null)
        {
            return RingoOutcome.Unchanged(this);
        }

        PendingCardId = null;
        CurrentPlayerId = RingoTurns.Next(this)?.PlayerId;

        return RingoOutcome.Done(this);
    }

    public const int KingsToEnd = 4;

    // Card ids run by suit, 13 ranks each, 2 to ace, so the king is the rank at index 11
    private const int KingRankIndex = 11;
}
