using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.System;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Repositories;

namespace PartyKing.Application.Accounts.Services;

public interface IAdminSeeder
{
    /// <summary>Creates the first admin from configuration when no admin account exists.</summary>
    Task SeedAsync(CancellationToken cancellationToken);
}

internal class AdminSeeder : IAdminSeeder
{
    private readonly IAccountsRepository _accountsRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly AdminConfiguration _config;
    private readonly ILogger<AdminSeeder> _logger;

    public AdminSeeder(
        IAccountsRepository accountsRepository,
        IPasswordHasher passwordHasher,
        IDateTimeProvider dateTimeProvider,
        IOptions<AdminConfiguration> config,
        ILogger<AdminSeeder> logger)
    {
        _accountsRepository = accountsRepository;
        _passwordHasher = passwordHasher;
        _dateTimeProvider = dateTimeProvider;
        _config = config.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await _accountsRepository.AnyAdminAsync(cancellationToken))
        {
            return;
        }

        var hasHash = !string.IsNullOrWhiteSpace(_config.PasswordHash);

        if (AccountUsernames.Validate(_config.Username) != AccountFailure.None
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
        var account = Account.Create(
            Guid.NewGuid(),
            _config.Username,
            passwordHash,
            AccountRole.Admin,
            Guid.NewGuid(),
            _dateTimeProvider.UtcNow.UtcDateTime).Account!;

        if (!await _accountsRepository.TryAddAsync(account, cancellationToken))
        {
            _logger.LogError(
                "The admin account '{Username}' was not created: a user has taken that username. Register no user under it, or change {Section}:Username",
                account.Username,
                AdminConfiguration.SectionName);
            return;
        }

        _logger.LogInformation("Created the admin account '{Username}'", account.Username);
    }
}
