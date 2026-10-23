namespace PartyKing.Application.Configuration;

/// <summary>Credentials used to create the first admin account when none exists yet.</summary>
public class AdminConfiguration
{
    public const string SectionName = "AdminSettings";

    public string? Username { get; set; }
    public string? Password { get; set; }
}
