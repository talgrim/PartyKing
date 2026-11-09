namespace PartyKing.Domain.Entities.Auth;

public enum AccountPasswordFailure
{
    None,
    TooShort,
    TooLong
}

/// <summary>A password that passed the account rule, or the reason it did not.</summary>
public readonly record struct AccountPasswordCreation(AccountPasswordFailure Failure, AccountPassword? Password);

/// <summary>
/// The password of a user account (8-100 characters, as typed: no trimming). The plain value only lives long enough
/// to be hashed; the account keeps the hash.
/// </summary>
public sealed record AccountPassword
{
    public const int MinLength = 8;
    public const int MaxLength = 100;

    private AccountPassword(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static AccountPasswordCreation Create(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return new AccountPasswordCreation(AccountPasswordFailure.TooShort, null);
        }

        return password.Length > MaxLength
            ? new AccountPasswordCreation(AccountPasswordFailure.TooLong, null)
            : new AccountPasswordCreation(AccountPasswordFailure.None, new AccountPassword(password));
    }

    /// <summary>Whether a typed password is short enough to be right. Logging in skips the hash for one that is not, so it is no cheap CPU burner.</summary>
    public static bool CanMatch(string? password)
    {
        return password is { Length: <= MaxLength };
    }
}
