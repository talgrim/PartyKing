using ErrorOr;

namespace PartyKing.Application.Sessions.Helpers;

internal static class SessionErrors
{
    public static Error NotFound() => Error.NotFound(description: "The session was not found");

    public static Error DeletionUnavailableInDemo() =>
        Error.Forbidden(description: "Demo sessions end on their own and cannot be deleted");
}
