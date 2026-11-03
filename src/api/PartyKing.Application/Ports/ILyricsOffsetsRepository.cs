namespace PartyKing.Application.Ports;

public interface ILyricsOffsetsRepository
{
    /// <summary>The saved offset of the song, or 0 when none was saved.</summary>
    Task<int> GetAsync(string songUrl, CancellationToken cancellationToken);

    Task SaveAsync(string songUrl, int offsetMs, CancellationToken cancellationToken);
}
