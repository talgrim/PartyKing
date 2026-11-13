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

    public static Error SessionGone()
    {
        return Error.NotFound(description: "That demo session is not there any more");
    }

    public static Error NotADemoSession()
    {
        return Error.NotFound(description: "That session is not a demo session");
    }

    public static Error PhotoTooLarge(long megabytes)
    {
        return Error.Validation(description: $"In the demo a photo can be at most {megabytes} MB");
    }

    public static Error TooManyPhotos(int limit)
    {
        return Error.Validation(description: $"In the demo a session can hold at most {limit} photos");
    }

    public static Error StartFailed()
    {
        return Error.Failure(description: "Could not start the demo");
    }
}
