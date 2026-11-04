using PartyKing.Domain.Entities.Lyrics;

namespace PartyKing.Domain.Repositories;

public interface ILyricsOffsetsRepository
{
    /// <summary>The saved offset of the song, or 0 when none was saved.</summary>
    Task<int> GetAsync(string songUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the offset of the song through <see cref="LyricsOffset.Change"/>, creating the row when there is none (two
    /// saves racing for a new song both succeed, the later one wins), and dispatches <c>LyricsOffsetChanged</c> once it is
    /// committed. A refused offset saves and dispatches nothing.
    /// </summary>
    Task<LyricsOffsetFailure> ChangeAsync(
        string songUrl,
        int offsetMs,
        Guid sessionId,
        int songId,
        CancellationToken cancellationToken);
}
