using ErrorOr;

namespace PartyKing.Application.Sessions.Helpers;

internal static class SessionPasswordPolicy
{
    public const int MinLength = 4;
    public const int MaxLength = 100;

    public static Error? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return Error.Validation(description: $"The password must be at least {MinLength} characters");
        }

        return password.Length > MaxLength
            ? Error.Validation(description: $"The password must be at most {MaxLength} characters")
            : null;
    }
}
