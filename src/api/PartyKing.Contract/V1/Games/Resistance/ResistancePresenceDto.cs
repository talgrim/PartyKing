namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>
/// The players whose device has the Resistance page open right now (a short reconnect does not count as gone). A
/// joined player who is not listed is offline. Pushed whole whenever it changes, so a missed push heals with the next.
/// </summary>
public record ResistancePresenceDto(IReadOnlyList<Guid> OnlinePlayerIds);
