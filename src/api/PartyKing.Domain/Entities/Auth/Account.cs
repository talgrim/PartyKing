namespace PartyKing.Domain.Entities.Auth;

public enum AccountFailure
{
    None,
    UsernameEmpty,
    UsernameTooLong
}

/// <summary>An account that was made, or the reason it was not.</summary>
public readonly record struct AccountCreation(AccountFailure Failure, Account? Account);

/// <summary>
/// A login: an admin (seeded from configuration) or a user (who registered). The security stamp is part of the
/// signed account cookie, so changing it signs the account out everywhere.
/// </summary>
public class Account
{
    private Account()
    {
    }

    /// <summary>The username is already normalized and checked by <see cref="Create"/>.</summary>
    internal Account(
        Guid id,
        string username,
        string passwordHash,
        AccountRole role,
        Guid securityStamp,
        DateTime createdAt)
    {
        Id = id;
        Username = username;
        PasswordHash = passwordHash;
        Role = role;
        SecurityStamp = securityStamp;
        CreatedAt = createdAt;
    }

    public Guid Id { get; internal set; }
    public string Username { get; internal set; } = null!;
    public string PasswordHash { get; internal set; } = null!;
    public AccountRole Role { get; internal set; }
    public Guid SecurityStamp { get; internal set; }
    public DateTime CreatedAt { get; internal set; }

    /// <summary>
    /// Makes an account under the trimmed username; a blank or too long one is refused. The id, the stamp and the time
    /// are arguments, like in the sessions (the service fills them from <c>Guid.NewGuid()</c> and
    /// <c>IDateTimeProvider</c>), and the hash is made by the caller (hashing is Application's).
    /// </summary>
    public static AccountCreation Create(
        Guid id,
        string? username,
        string passwordHash,
        AccountRole role,
        Guid securityStamp,
        DateTime now)
    {
        var failure = AccountUsernames.Validate(username);

        if (failure != AccountFailure.None)
        {
            return new AccountCreation(failure, null);
        }

        return new AccountCreation(
            AccountFailure.None,
            new Account(id, AccountUsernames.Normalize(username), passwordHash, role, securityStamp, now));
    }

    /// <summary>Whether a cookie that carries this stamp still belongs to the account.</summary>
    public bool IsSignedInBy(Guid securityStamp)
    {
        return SecurityStamp == securityStamp;
    }

    /// <summary>Replaces the hash and the stamp, so every cookie issued before is dead.</summary>
    public void ChangePassword(string passwordHash, Guid newSecurityStamp)
    {
        PasswordHash = passwordHash;
        SecurityStamp = newSecurityStamp;
    }

    /// <summary>Replaces the stamp only: signs the account out everywhere.</summary>
    public void SignOutEverywhere(Guid newSecurityStamp)
    {
        SecurityStamp = newSecurityStamp;
    }
}
