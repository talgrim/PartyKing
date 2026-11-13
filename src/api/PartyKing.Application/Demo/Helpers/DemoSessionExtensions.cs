using PartyKing.Domain.Entities.Demo;

namespace PartyKing.Application.Demo.Helpers;

internal static class DemoSessionExtensions
{
    /// <summary>The stored UTC time as an instant, which is what the callers and the DTOs speak.</summary>
    public static DateTimeOffset ExpiresAtUtc(this DemoSession demoSession)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(demoSession.ExpiresAt, DateTimeKind.Utc));
    }
}
