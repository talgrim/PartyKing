namespace PartyKing.Application.Configuration;

/// <summary>Credentials used to create the first admin account when none exists yet.</summary>
public class AdminConfiguration
{
    public const string SectionName = "AdminSettings";

    public string? Username { get; set; }

    /// <summary>Plain-text password; it is hashed before it is stored. Ignored when <see cref="PasswordHash"/> is set.</summary>
    public string? Password { get; set; }

    /// <summary>Ready-made hash (see <c>hash-password</c> in Program.cs), so no plain text has to sit in the settings.</summary>
    public string? PasswordHash { get; set; }
}
