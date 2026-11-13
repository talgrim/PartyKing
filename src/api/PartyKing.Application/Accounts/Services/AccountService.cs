using ErrorOr;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Demo.Helpers;
using PartyKing.Application.Accounts.Helpers;
using PartyKing.Application.Mappings;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Accounts;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Repositories;

namespace PartyKing.Application.Accounts.Services;

public interface IAccountService
{
    /// <summary>Makes a user account; the caller signs the new user in.</summary>
    Task<ErrorOr<AccountIdentity>> RegisterAsync(string? username, string? password, CancellationToken cancellationToken);

    /// <summary>Checks the credentials of a user or an admin and returns who they are; the caller signs them in.</summary>
    Task<ErrorOr<AccountIdentity>> LoginAsync(string? username, string? password, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the account still exists and the stamp is still its stamp; the identity (with the role as it is now)
    /// or null. The cookie handler asks on every request, so a deleted account, a changed password and a sign-out
    /// everywhere all end the cookie.
    /// </summary>
    Task<AccountIdentity?> ValidateAsync(Guid accountId, Guid securityStamp, CancellationToken cancellationToken);

    Task<ErrorOr<AccountDto>> GetAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Changes the caller's own password. Returns the new identity (new stamp), so the caller stays signed in.</summary>
    Task<ErrorOr<AccountIdentity>> ChangePasswordAsync(
        Guid accountId,
        string? currentPassword,
        string? newPassword,
        CancellationToken cancellationToken);

    /// <summary>Changes the stamp, which ends every cookie of the account, this device's too.</summary>
    Task<ErrorOr<Success>> SignOutEverywhereAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Deletes the caller's own user account after the password is confirmed. Admins cannot delete theirs.</summary>
    Task<ErrorOr<Success>> DeleteAsync(Guid accountId, string? password, CancellationToken cancellationToken);
}

internal class AccountService : IAccountService
{
    private readonly IAccountsRepository _accountsRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DemoConfiguration _demoConfiguration;

    // Verified against when the username is unknown, so both failures cost the same time
    private readonly AccountDummyHash _dummyHash;

    public AccountService(
        IAccountsRepository accountsRepository,
        IPasswordHasher passwordHasher,
        AccountDummyHash dummyHash,
        IDateTimeProvider dateTimeProvider,
        IOptions<DemoConfiguration> demoOptions)
    {
        _accountsRepository = accountsRepository;
        _passwordHasher = passwordHasher;
        _dummyHash = dummyHash;
        _dateTimeProvider = dateTimeProvider;
        _demoConfiguration = demoOptions.Value;
    }

    public async Task<ErrorOr<AccountIdentity>> RegisterAsync(
        string? username,
        string? password,
        CancellationToken cancellationToken)
    {
        // Accounts are permanent rows, and a demo is a throw-away
        if (_demoConfiguration.Enabled)
        {
            return DemoErrors.NotAvailableInDemo("Signing up");
        }

        var passwordCreation = AccountPassword.Create(password);

        if (passwordCreation.Password is null)
        {
            return AccountErrors.From(passwordCreation.Failure);
        }

        var creation = Account.Create(
            Guid.NewGuid(),
            username,
            _passwordHasher.Hash(passwordCreation.Password.Value, PasswordHasher.UserIterations),
            AccountRole.User,
            Guid.NewGuid(),
            _dateTimeProvider.UtcNow.UtcDateTime);

        if (creation.Account is null)
        {
            return AccountErrors.From(creation.Failure);
        }

        if (!await _accountsRepository.TryAddAsync(creation.Account, cancellationToken))
        {
            return AccountErrors.UsernameTaken();
        }

        return IdentityOf(creation.Account);
    }

    public async Task<ErrorOr<AccountIdentity>> LoginAsync(
        string? username,
        string? password,
        CancellationToken cancellationToken)
    {
        // A password that long is nobody's; no hash is spent on it
        if (!AccountPassword.CanMatch(password))
        {
            return AccountErrors.InvalidCredentials();
        }

        // A blank username is nobody's; the rule (trimming) is the Domain's
        var normalized = AccountUsernames.Normalize(username);
        var account = normalized.Length == 0
            ? null
            : await _accountsRepository.GetByUsernameAsync(normalized, cancellationToken);

        var valid = _passwordHasher.Verify(password ?? string.Empty, account?.PasswordHash ?? _dummyHash.Value);

        if (account is null || !valid)
        {
            return AccountErrors.InvalidCredentials();
        }

        return IdentityOf(account);
    }

    public async Task<AccountIdentity?> ValidateAsync(
        Guid accountId,
        Guid securityStamp,
        CancellationToken cancellationToken)
    {
        var account = await _accountsRepository.GetAsync(accountId, cancellationToken);

        return account is not null && account.IsSignedInBy(securityStamp) ? IdentityOf(account) : null;
    }

    public async Task<ErrorOr<AccountDto>> GetAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await _accountsRepository.GetAsync(accountId, cancellationToken);

        return account is null ? AccountErrors.NotFound() : account.ToDto();
    }

    public async Task<ErrorOr<AccountIdentity>> ChangePasswordAsync(
        Guid accountId,
        string? currentPassword,
        string? newPassword,
        CancellationToken cancellationToken)
    {
        var account = await _accountsRepository.GetAsync(accountId, cancellationToken);

        if (account is null)
        {
            return AccountErrors.NotFound();
        }

        var newPasswordCreation = AccountPassword.Create(newPassword);

        if (newPasswordCreation.Password is null)
        {
            return AccountErrors.From(newPasswordCreation.Failure);
        }

        if (!IsPasswordOf(account, currentPassword))
        {
            return AccountErrors.WrongCurrentPassword();
        }

        var newStamp = Guid.NewGuid();
        var newHash = _passwordHasher.Hash(newPasswordCreation.Password.Value, PasswordHasher.UserIterations);

        if (!await _accountsRepository.ChangePasswordAsync(account.Id, newHash, newStamp, cancellationToken))
        {
            return AccountErrors.NotFound();
        }

        return new AccountIdentity(account.Id, account.Role, newStamp);
    }

    public async Task<ErrorOr<Success>> SignOutEverywhereAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var signedOut = await _accountsRepository.SignOutEverywhereAsync(accountId, Guid.NewGuid(), cancellationToken);

        return signedOut ? Result.Success : AccountErrors.NotFound();
    }

    public async Task<ErrorOr<Success>> DeleteAsync(
        Guid accountId,
        string? password,
        CancellationToken cancellationToken)
    {
        var account = await _accountsRepository.GetAsync(accountId, cancellationToken);

        if (account is null)
        {
            return AccountErrors.NotFound();
        }

        if (account.Role == AccountRole.Admin)
        {
            return AccountErrors.AdminNotAllowed("Deleting the account");
        }

        if (!IsPasswordOf(account, password))
        {
            return AccountErrors.WrongPassword();
        }

        return await _accountsRepository.DeleteAsync(accountId, cancellationToken)
            ? Result.Success
            : AccountErrors.NotFound();
    }

    private bool IsPasswordOf(Account account, string? password)
    {
        return AccountPassword.CanMatch(password) && _passwordHasher.Verify(password ?? string.Empty, account.PasswordHash);
    }

    private static AccountIdentity IdentityOf(Account account)
    {
        return new AccountIdentity(account.Id, account.Role, account.SecurityStamp);
    }
}
