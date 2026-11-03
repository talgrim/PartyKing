using PartyKing.Domain.Entities.Auth;

namespace PartyKing.Domain.Repositories;

public interface IAdminAccountsRepository
{
    Task<bool> AnyAsync(CancellationToken cancellationToken);
    Task<AdminAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<AdminAccount?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(AdminAccount account, CancellationToken cancellationToken);
}
