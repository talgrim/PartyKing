using ErrorOr;
using PartyKing.Domain.Entities.Sessions;

namespace PartyKing.Application.Sessions.Helpers;

/// <summary>Maps the failures of the Domain's <see cref="SessionPassword"/> rule to errors.</summary>
internal static class SessionPasswordErrors
{
    public static Error From(SessionPasswordFailure failure)
    {
        return failure switch
        {
            SessionPasswordFailure.TooLong => Error.Validation(
                description: $"The password must be at most {SessionPassword.MaxLength} characters"),
            _ => Error.Validation(
                description: $"The password must be at least {SessionPassword.MinLength} characters")
        };
    }
}
