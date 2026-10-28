namespace PartyKing.Contract.V1.Player;

/// <param name="Error">Why the fallback playlist could not add a song; null once it works again.</param>
public record FallbackErrorDto(string? Error);
