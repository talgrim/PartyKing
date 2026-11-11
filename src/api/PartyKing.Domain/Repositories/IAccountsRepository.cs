using PartyKing.Domain.Entities.Auth;

namespace PartyKing.Domain.Repositories;

/// <summary>One page of the users (accounts with the <see cref="AccountRole.User"/> role) and how many there are in all.</summary>
public sealed record AccountPage(IReadOnlyList<Account> Items, int TotalCount);

public interface IAccountsRepository
{
    Task<bool> AnyAdminAsync(CancellationToken cancellationToken);
    Task<Account?> GetByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<Account?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Adds the account; false when the username is already taken (the unique index decides, whatever the case).</summary>
    Task<bool> TryAddAsync(Account account, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the password hash and the stamp of the account. False when the account is gone. The stamp changes with
    /// it, so every cookie issued before stops working.
    /// </summary>
    Task<bool> ChangePasswordAsync(
        Guid id,
        string passwordHash,
        Guid newSecurityStamp,
        CancellationToken cancellationToken);

    /// <summary>Replaces the stamp of the account (signs it out everywhere). False when the account is gone.</summary>
    Task<bool> SignOutEverywhereAsync(Guid id, Guid newSecurityStamp, CancellationToken cancellationToken);

    /// <summary>Deletes the account; false when it was already gone.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The users (never the admins), ordered by username. <paramref name="search"/> keeps the usernames that contain it,
    /// whatever the case. The page is 1-based; the caller keeps it and the size in range.
    /// </summary>
    Task<AccountPage> GetUsersPageAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
