namespace PartyKing.Application.Configuration;

/// <summary>
/// When a session whose deletion a guest asked for is deleted, and how often the sweep looks for the ones that are due.
/// The defaults are the ones the privacy page promises; the short values are for tests and the e2e environment.
/// </summary>
public class SessionDeletionConfiguration
{
    public const string SectionName = "SessionDeletionSettings";

    /// <summary>Days between a guest's request and the deletion. Fractions are allowed, so a test can use seconds.</summary>
    public double DelayDays { get; set; } = 7;

    /// <summary>How often the sweep looks for sessions whose deletion is due.</summary>
    public int SweepIntervalSeconds { get; set; } = 300;

    public TimeSpan Delay => TimeSpan.FromDays(DelayDays);

    public TimeSpan SweepInterval => TimeSpan.FromSeconds(SweepIntervalSeconds);
}
