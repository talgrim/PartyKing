namespace PartyKing.Domain.Entities.Playlists;

/// <summary>A saved playlist without its songs, for listing the playlists of an account.</summary>
public sealed record SavedPlaylistSummary(Guid Id, string Name, DateTime CreatedAt, int SongCount);
