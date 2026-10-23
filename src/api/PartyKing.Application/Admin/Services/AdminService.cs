using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Domain.Entities;
using PartyKing.Infrastructure.Authorization;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Admin.Services;

public interface IAdminService
{
    Task<ErrorOr<Success>> LoginAsync(string? username, string? password, CancellationToken cancellationToken);
    void Logout();
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Creates the first admin from configuration when no admin account exists.</summary>
    Task SeedAsync(CancellationToken cancellationToken);
}

internal class AdminService : IAdminService
{
    private readonly IAdminAccountsRepository _accountsRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;
    private readonly AdminConfiguration _config;
    private readonly ILogger<AdminService> _logger;

    // Verified against when the username is unknown, so both failures cost the same time
    private readonly string _dummyHash;

    public AdminService(
        IAdminAccountsRepository accountsRepository,
        IPasswordHasher passwordHasher,
        IHttpContextAccessor httpContextAccessor,
        ICookieAuthorizationHandler cookieAuthorizationHandler,
        IOptions<AdminConfiguration> config,
        ILogger<AdminService> logger)
    {
        _accountsRepository = accountsRepository;
        _passwordHasher = passwordHasher;
        _httpContextAccessor = httpContextAccessor;
        _cookieAuthorizationHandler = cookieAuthorizationHandler;
        _config = config.Value;
        _logger = logger;
        _dummyHash = passwordHasher.Hash(Guid.NewGuid().ToString());
    }

    public async Task<ErrorOr<Success>> LoginAsync(
        string? username,
        string? password,
        CancellationToken cancellationToken)
    {
        var account = string.IsNullOrWhiteSpace(username)
            ? null
            : await _accountsRepository.GetByUsernameAsync(username.Trim(), cancellationToken);

        var valid = _passwordHasher.Verify(password ?? string.Empty, account?.PasswordHash ?? _dummyHash);

        if (account is null || !valid)
        {
            return Error.Unauthorized(description: "Invalid username or password");
        }

        _cookieAuthorizationHandler.AuthorizeAdmin(account.Id, _httpContextAccessor.HttpContext);

        return Result.Success;
    }

    public void Logout()
    {
        _cookieAuthorizationHandler.SignOutAdmin(_httpContextAccessor.HttpContext);
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

        if (string.IsNullOrWhiteSpace(_config.Username) || string.IsNullOrWhiteSpace(_config.Password))
        {
            _logger.LogWarning(
                "No admin account exists. Set {Section}:Username and {Section}:Password in appsettings.Local.json " +
                "and restart to create one",
                AdminConfiguration.SectionName,
                AdminConfiguration.SectionName);
            return;
        }

        var account = new AdminAccount(_config.Username.Trim(), _passwordHasher.Hash(_config.Password));

        await _accountsRepository.AddAsync(account, cancellationToken);

        _logger.LogInformation("Created the admin account '{Username}'", account.Username);
    }
}
