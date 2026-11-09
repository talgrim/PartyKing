using ErrorOr;
using PartyKing.Domain.Entities.Auth;

namespace PartyKing.Application.Accounts.Helpers;

/// <summary>Maps the failures of the Domain's account rules to errors, and holds the errors the account services share.</summary>
internal static class AccountErrors
{
    public static Error NotFound() => Error.NotFound(description: "The account was not found");

    public static Error InvalidCredentials() => Error.Unauthorized(description: "Invalid username or password");

    public static Error UsernameTaken() => Error.Conflict(description: "The username is already taken");

    public static Error WrongPassword() => Error.Validation(description: "The password is incorrect");

    public static Error WrongCurrentPassword() => Error.Validation(description: "The current password is incorrect");

    public static Error AdminNotAllowed(string what) => Error.Forbidden(description: $"{what} is not available for admin accounts");

    public static Error From(AccountFailure failure)
    {
        return failure switch
        {
            AccountFailure.UsernameTooLong => Error.Validation(
                description: $"The username must be at most {AccountUsernames.MaxLength} characters"),
            _ => Error.Validation(description: "The username must not be blank")
        };
    }

    public static Error From(AccountPasswordFailure failure)
    {
        return failure switch
        {
            AccountPasswordFailure.TooLong => Error.Validation(
                description: $"The password must be at most {AccountPassword.MaxLength} characters"),
            _ => Error.Validation(
                description: $"The password must be at least {AccountPassword.MinLength} characters")
        };
    }
}
