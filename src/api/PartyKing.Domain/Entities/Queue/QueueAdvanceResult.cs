namespace PartyKing.Domain.Entities.Queue;

/// <summary>What an advance of the queue did.</summary>
/// <param name="Playing">The song playing afterwards, or null when there is none.</param>
/// <param name="Advanced">False when the caller's view was outdated and nothing changed.</param>
/// <param name="PreviousSongId">The song that was marked played; null when nothing was playing or nothing advanced.</param>
public sealed record QueueAdvanceResult(QueueItem? Playing, bool Advanced, int? PreviousSongId);
