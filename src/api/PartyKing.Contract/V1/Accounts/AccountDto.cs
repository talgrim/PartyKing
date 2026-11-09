using PartyKing.Contract.V1.Enums;

namespace PartyKing.Contract.V1.Accounts;

public record AccountDto(Guid Id, string Username, AccountRole Role, DateTime CreatedAt);
