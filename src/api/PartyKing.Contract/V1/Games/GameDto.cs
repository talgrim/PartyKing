namespace PartyKing.Contract.V1.Games;

/// <param name="Id">Stable slug identifying the game.</param>
public record GameDto(string Id, string Name, string Description);
