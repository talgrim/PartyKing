using PartyKing.Contract.V1.Enums;

namespace PartyKing.Contract.V1.Queue;

public record SongDataDto(SongSource Source, string? Url, string Name)
{
    public static SongDataDto Spotify(string name) => new(SongSource.Spotify, null, name);
}