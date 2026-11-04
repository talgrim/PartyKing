namespace PartyKing.Application.Spotify.Models;

/// <summary>A fresh Spotify access token for a host and how many seconds it stays valid.</summary>
public record SpotifyAccessGrant(string AccessToken, int ExpiresIn);
