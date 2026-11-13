namespace PartyKing.Application.Demo.Models;

/// <summary>A demo session a browser says it started, as its cookie remembers it; nothing here was checked yet.</summary>
public record StartedDemoSession(Guid SessionId, string ProfileId);
