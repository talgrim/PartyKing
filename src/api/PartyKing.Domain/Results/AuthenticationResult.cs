namespace PartyKing.Domain.Results;

public record AuthenticationResult(
    string ProfileId,
    string DisplayName,
    string AccessToken,
    string RefreshToken,
    int ExpiresIn);
