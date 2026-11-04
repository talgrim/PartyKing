using PartyKing.Domain.Repositories;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Domain.Entities.Auth;

namespace PartyKing.Application.Admin.Services;

public interface IAdminService
{
    /// <summary>Checks the credentials and returns the id of the admin account; the caller signs the admin in.</summary>
    Task<ErrorOr<Guid>> LoginAsync(string? username, string? password, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Creates the first admin from configuration when no admin account exists.</summary>
    Task SeedAsync(CancellationToken cancellationToken);
}

internal class AdminService : IAdminService
{
    private readonly IAdminAccountsRepository _accountsRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AdminConfiguration _config;
    private readonly ILogger<AdminService> _logger;

    // Verified against when the username is unknown, so both failures cost the same time
    private readonly AdminDummyHash _dummyHash;

    public AdminService(
        IAdminAccountsRepository accountsRepository,
        IPasswordHasher passwordHasher,
        AdminDummyHash dummyHash,
        IOptions<AdminConfiguration> config,
        ILogger<AdminService> logger)
    {
        _accountsRepository = accountsRepository;
        _passwordHasher = passwordHasher;
        _config = config.Value;
        _logger = logger;
        _dummyHash = dummyHash;
    }

    public async Task<ErrorOr<Guid>> LoginAsync(
        string? username,
        string? password,
        CancellationToken cancellationToken)
    {
        // A blank username is nobody's; the rule (trimming) is the aggregate's
        var normalized = AdminUsernames.Normalize(username);
        var account = normalized.Length == 0
            ? null
            : await _accountsRepository.GetByUsernameAsync(normalized, cancellationToken);

        var valid = _passwordHasher.Verify(password ?? string.Empty, account?.PasswordHash ?? _dummyHash.Value);

        if (account is null || !valid)
        {
            return Error.Unauthorized(description: "Invalid username or password");
        }

        return account.Id;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _accountsRepository.GetAsync(id, cancellationToken) is not null;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await _accountsRepository.AnyAsync(cancellationToken))
        {
            return;
        }

        var hasHash = !string.IsNullOrWhiteSpace(_config.PasswordHash);

        if (AdminUsernames.Validate(_config.Username) != AdminAccountFailure.None
            || (!hasHash && string.IsNullOrWhiteSpace(_config.Password)))
        {
            _logger.LogWarning(
                "No admin account exists. Set {Section}:Username and {Section}:Password (or {Section}:PasswordHash) " +
                "in appsettings.Local.json and restart to create one",
                AdminConfiguration.SectionName,
                AdminConfiguration.SectionName,
                AdminConfiguration.SectionName);
            return;
        }

        if (hasHash && !_passwordHasher.IsHash(_config.PasswordHash!.Trim()))
        {
            _logger.LogError(
                "{Section}:PasswordHash is not a valid hash. Generate one with: dotnet run --project PartyKing.API -- hash-password",
                AdminConfiguration.SectionName);
            return;
        }

        var passwordHash = hasHash ? _config.PasswordHash!.Trim() : _passwordHasher.Hash(_config.Password!);
        var account = AdminAccount.Create(Guid.NewGuid(), _config.Username, passwordHash).Account!;

        await _accountsRepository.AddAsync(account, cancellationToken);

        _logger.LogInformation("Created the admin account '{Username}'", account.Username);
    }
}
