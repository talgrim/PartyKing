namespace PartyKing.Domain.Results;

public record AuthenticationResult(string ProfileId, string AccessToken, string RefreshToken, int ExpiresIn);