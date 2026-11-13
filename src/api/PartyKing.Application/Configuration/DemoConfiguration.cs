namespace PartyKing.Application.Configuration;

/// <summary>
/// The demo mode: a server with no Spotify where each visitor starts a throw-away session of their own and hosts it.
/// Off unless a deployment turns it on.
/// </summary>
public class DemoConfiguration
{
    public const string SectionName = "DemoSettings";

    public bool Enabled { get; set; }

    /// <summary>How long a demo session lives before it is deleted.</summary>
    public int LifetimeMinutes { get; set; } = 180;

    /// <summary>How many demo sessions may exist at once; starting another one is refused until some expire.</summary>
    public int MaxSessions { get; set; } = 50;

    /// <summary>How many demo sessions one IP address may start per hour.</summary>
    public int StartsPerHourPerIp { get; set; } = 5;

    public int MaxPhotosPerSession { get; set; } = 10;

    /// <summary>The size of one photo, below the 20 MB an ordinary session allows.</summary>
    public long MaxPhotoBytes { get; set; } = 5L * 1024 * 1024;
}
