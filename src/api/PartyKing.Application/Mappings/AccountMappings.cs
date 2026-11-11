using PartyKing.Contract.V1.Accounts;
using PartyKing.Domain.Entities.Auth;
using Riok.Mapperly.Abstractions;

namespace PartyKing.Application.Mappings;

/// <summary>The DTO has no member for the hash or the stamp, so neither can leave through this mapping.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class AccountMappings
{
    public static partial AccountDto ToDto(this Account account);
}
