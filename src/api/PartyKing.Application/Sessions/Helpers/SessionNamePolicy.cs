using ErrorOr;
using PartyKing.Domain.Entities.Sessions;

namespace PartyKing.Application.Sessions.Helpers;

/// <summary>Maps the Domain's <see cref="SessionNames"/> rule to errors.</summary>
internal static class SessionNamePolicy
{
    public const int MaxLength = SessionNames.MaxLength;

    /// <summary>
    /// Null when the name is acceptable. A blank name is fine when <paramref name="allowBlank"/> (create falls back to
    /// the default name) and an error otherwise (rename).
    /// </summary>
    public static Error? Validate(string? name, bool allowBlank)
    {
        return SessionNames.Validate(name) switch
        {
            SessionNameFailure.TooLong => TooLong(),
            SessionNameFailure.Empty when !allowBlank => Error.Validation(description: "The session name must not be empty"),
            _ => null
        };
    }

    public static Error TooLong() =>
        Error.Validation(description: $"The session name must be at most {MaxLength} characters");
}
