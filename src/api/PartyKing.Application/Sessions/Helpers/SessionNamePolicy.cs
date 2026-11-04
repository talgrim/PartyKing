using ErrorOr;
using PartyKing.Domain.Entities.Sessions;

namespace PartyKing.Application.Sessions.Helpers;

/// <summary>Maps the failures of the Domain's name rule (<see cref="SessionNames"/>) to errors.</summary>
internal static class SessionNamePolicy
{
    public static Error ToError(SessionNameFailure failure)
    {
        return failure switch
        {
            SessionNameFailure.TooLong => Error.Validation(
                description: $"The session name must be at most {SessionNames.MaxLength} characters"),
            _ => Error.Validation(description: "The session name must not be empty")
        };
    }
}
