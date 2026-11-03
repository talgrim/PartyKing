namespace PartyKing.Domain.Entities.Sessions;

public enum SessionNameFailure
{
    None,
    Empty,
    TooLong
}

/// <summary>The one rule for session names, used by create and rename: the name is trimmed, then its length is checked.</summary>
public static class SessionNames
{
    public const int MaxLength = 60;
    public const string DefaultName = "New session";

    /// <summary>Checks the trimmed name. A blank name is <see cref="SessionNameFailure.Empty"/>; callers that accept one fall back to <see cref="DefaultName"/>.</summary>
    public static SessionNameFailure Validate(string? name)
    {
        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return SessionNameFailure.Empty;
        }

        return trimmed.Length > MaxLength ? SessionNameFailure.TooLong : SessionNameFailure.None;
    }

    /// <summary>The trimmed name, or <see cref="DefaultName"/> when it is blank.</summary>
    public static string Normalize(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim();
    }
}
