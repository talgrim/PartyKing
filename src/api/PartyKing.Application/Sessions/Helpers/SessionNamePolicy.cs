using ErrorOr;

namespace PartyKing.Application.Sessions.Helpers;

internal static class SessionNamePolicy
{
    public const int MaxLength = 60;

    public static Error TooLong() =>
        Error.Validation(description: $"The session name must be at most {MaxLength} characters");
}
