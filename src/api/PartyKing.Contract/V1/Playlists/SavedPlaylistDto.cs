namespace PartyKing.Contract.V1.Playlists;

/// <param name="SongCount">How many songs were saved, after de-duplication.</param>
public record SavedPlaylistDto(Guid Id, string Name, DateTime CreatedAt, int SongCount);
