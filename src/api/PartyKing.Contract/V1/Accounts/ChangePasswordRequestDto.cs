namespace PartyKing.Contract.V1.Accounts;

public record ChangePasswordRequestDto(string CurrentPassword, string NewPassword);
