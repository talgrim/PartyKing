namespace PartyKing.Domain.Entities.Auth;

public enum AdminAccountFailure
{
    None,
    UsernameEmpty
}

/// <summary>An admin account that was made, or the reason it was not.</summary>
public readonly record struct AdminAccountCreation(AdminAccountFailure Failure, AdminAccount? Account);

/// <summary>The one rule for admin usernames, used by seeding and login: the name is trimmed and must not be blank.</summary>
public static class AdminUsernames
{
    /// <summary>The trimmed name; empty when it is blank (nobody can log in under that, nor be seeded with it).</summary>
    public static string Normalize(string? username)
    {
        return username?.Trim() ?? string.Empty;
    }

    public static AdminAccountFailure Validate(string? username)
    {
        return Normalize(username).Length == 0 ? AdminAccountFailure.UsernameEmpty : AdminAccountFailure.None;
    }
}

public class AdminAccount
{
    private AdminAccount()
    {
    }

    /// <summary>The username is already normalized and checked by <see cref="Create"/>.</summary>
    internal AdminAccount(Guid id, string username, string passwordHash)
    {
        Id = id;
        Username = username;
        PasswordHash = passwordHash;
    }

    public Guid Id { get; internal set; }
    public string Username { get; internal set; } = null!;
    public string PasswordHash { get; internal set; } = null!;

    /// <summary>
    /// Makes an account under the trimmed username; a blank one is refused. The id is an argument, like in the sessions
    /// (the service fills it from <c>Guid.NewGuid()</c>), and the hash is made by the caller (hashing is Application's).
    /// </summary>
    public static AdminAccountCreation Create(Guid id, string? username, string passwordHash)
    {
        if (AdminUsernames.Validate(username) != AdminAccountFailure.None)
        {
            return new AdminAccountCreation(AdminAccountFailure.UsernameEmpty, null);
        }

        return new AdminAccountCreation(
            AdminAccountFailure.None,
            new AdminAccount(id, AdminUsernames.Normalize(username), passwordHash));
    }
}
