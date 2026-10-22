namespace PartyKing.Domain.Results;

public record ReAuthenticationResult(string AccessToken, string RefreshToken, int ExpiresIn);