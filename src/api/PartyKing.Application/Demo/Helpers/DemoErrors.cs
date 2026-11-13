using ErrorOr;

namespace PartyKing.Application.Demo.Helpers;

internal static class DemoErrors
{
    public static Error NotEnabled()
    {
        return Error.NotFound(description: "The demo is not enabled");
    }

    public static Error Full()
    {
        return Error.Conflict(description: "The demo is full right now, try again in a few minutes");
    }

    public static Error NotAvailableInDemo(string what)
    {
        return Error.Forbidden(description: $"{what} is not available in the demo");
    }

    public static Error StartFailed()
    {
        return Error.Failure(description: "Could not start the demo");
    }
}
