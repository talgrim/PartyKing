namespace PartyKing.Contract.V1.Queue;

/// <param name="PreviousSongId">The song that was Playing before the advance; null when nothing was.</param>
/// <param name="NowPlaying">The song that is Playing afterwards; null when the queue emptied.</param>
public record QueueAdvancedDto(int? PreviousSongId, QueueItemDto? NowPlaying);
