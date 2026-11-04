namespace PartyKing.Application.Spotify.Models;

/// <summary>A completed host login: who logged in, the Spotify access token and how long it stays valid.</summary>
public record HostLogin(string ProfileId, string AccessToken, int ExpiresIn, Guid SessionId);
