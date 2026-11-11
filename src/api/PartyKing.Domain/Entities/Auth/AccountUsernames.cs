namespace PartyKing.Domain.Entities.Auth;

/// <summary>The one rule for account usernames, used by registration, seeding and login: the name is trimmed, not blank and at most <see cref="MaxLength"/> characters.</summary>
public static class AccountUsernames
{
    public const int MaxLength = 50;

    /// <summary>The trimmed name; empty when it is blank (nobody can log in under that, nor be registered with it).</summary>
    public static string Normalize(string? username)
    {
        return username?.Trim() ?? string.Empty;
    }

    public static AccountFailure Validate(string? username)
    {
        var normalized = Normalize(username);

        if (normalized.Length == 0)
        {
            return AccountFailure.UsernameEmpty;
        }

        return normalized.Length > MaxLength ? AccountFailure.UsernameTooLong : AccountFailure.None;
    }
}
