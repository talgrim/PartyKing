using ErrorOr;
using PartyKing.Application.Accounts.Helpers;
using PartyKing.Application.Mappings;
using PartyKing.Contract.V1.Accounts;
using PartyKing.Contract.V1.Common;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Repositories;

namespace PartyKing.Application.Accounts.Services;

/// <summary>What an admin does with the users. Whether the caller is an admin is the API's to check ([AuthorizeAdmin]).</summary>
public interface IAccountAdminService
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>One page of the users (never the admins), ordered by username. A page or size out of range is clamped.</summary>
    Task<PagedDto<AccountDto>> ListUsersAsync(
        string? search,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken);

    /// <summary>Sets a new password for a user, which also signs the user out everywhere. Admin passwords are not reset here.</summary>
    Task<ErrorOr<Success>> ResetPasswordAsync(Guid accountId, string? newPassword, CancellationToken cancellationToken);
}

internal class AccountAdminService : IAccountAdminService
{
    private readonly IAccountsRepository _accountsRepository;
    private readonly IPasswordHasher _passwordHasher;

    public AccountAdminService(IAccountsRepository accountsRepository, IPasswordHasher passwordHasher)
    {
        _accountsRepository = accountsRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<PagedDto<AccountDto>> ListUsersAsync(
        string? search,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var actualPage = Math.Max(page ?? 1, 1);
        var actualPageSize = Math.Clamp(pageSize ?? IAccountAdminService.DefaultPageSize, 1, IAccountAdminService.MaxPageSize);

        var result = await _accountsRepository.GetUsersPageAsync(search, actualPage, actualPageSize, cancellationToken);

        return new PagedDto<AccountDto>(
            result.Items.Select(x => x.ToDto()).ToList(),
            actualPage,
            actualPageSize,
            result.TotalCount);
    }

    public async Task<ErrorOr<Success>> ResetPasswordAsync(
        Guid accountId,
        string? newPassword,
        CancellationToken cancellationToken)
    {
        var account = await _accountsRepository.GetAsync(accountId, cancellationToken);

        if (account is null)
        {
            return AccountErrors.NotFound();
        }

        if (account.Role == AccountRole.Admin)
        {
            return AccountErrors.AdminNotAllowed("Resetting the password");
        }

        var creation = AccountPassword.Create(newPassword);

        if (creation.Password is null)
        {
            return AccountErrors.From(creation.Failure);
        }

        var hash = _passwordHasher.Hash(creation.Password.Value, PasswordHasher.UserIterations);
        var changed = await _accountsRepository.ChangePasswordAsync(account.Id, hash, Guid.NewGuid(), cancellationToken);

        return changed ? Result.Success : AccountErrors.NotFound();
    }
}
