namespace PartyKing.Domain.Entities.Sessions;

public enum SessionPasswordFailure
{
    None,
    TooShort,
    TooLong
}

/// <summary>A password that passed the session rule, or the reason it did not.</summary>
public readonly record struct SessionPasswordCreation(SessionPasswordFailure Failure, SessionPassword? Password);

/// <summary>
/// The password everyone must enter to join a session (4-100 characters, as typed: no trimming). The plain value only
/// lives long enough to be hashed; the session keeps the hash.
/// </summary>
public sealed record SessionPassword
{
    public const int MinLength = 4;
    public const int MaxLength = 100;

    private SessionPassword(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static SessionPasswordCreation Create(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return new SessionPasswordCreation(SessionPasswordFailure.TooShort, null);
        }

        return password.Length > MaxLength
            ? new SessionPasswordCreation(SessionPasswordFailure.TooLong, null)
            : new SessionPasswordCreation(SessionPasswordFailure.None, new SessionPassword(password));
    }

    /// <summary>Whether a typed password is short enough to be right. Joining skips the hash for one that is not, so it is no cheap CPU burner.</summary>
    public static bool CanMatch(string? password)
    {
        return password is { Length: <= MaxLength };
    }
}
