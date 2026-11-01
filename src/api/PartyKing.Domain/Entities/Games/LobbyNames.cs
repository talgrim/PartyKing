namespace PartyKing.Domain.Entities.Games;

/// <summary>Why a name typed by a guest is not accepted as a player name.</summary>
public enum LobbyNameFailure
{
    Empty,
    TooLong,

    /// <summary>The name ends with "(host)", which is how everyone recognizes the session host.</summary>
    HostSuffix,
}

/// <summary>The rules for the names of lobby players.</summary>
public static class LobbyNames
{
    public const int MaxLength = 30;

    private const string FallbackHostName = "Host";

    /// <summary>The first word of the Spotify display name (all of it when it has no spaces).</summary>
    public static string HostName(string? displayName)
    {
        var first = displayName?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (first is null)
        {
            return FallbackHostName;
        }

        return first.Length > MaxLength ? first[..MaxLength] : first;
    }

    /// <summary>Checks a trimmed name a guest or an admin typed; null when it is fine.</summary>
    public static LobbyNameFailure? Validate(string name)
    {
        if (name.Length == 0)
        {
            return LobbyNameFailure.Empty;
        }

        if (name.Length > MaxLength)
        {
            return LobbyNameFailure.TooLong;
        }

        // The suffix is how everyone recognizes the host, so a guest can't spell it
        if (name.EndsWith(LobbyPlayer.HostSuffix.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return LobbyNameFailure.HostSuffix;
        }

        return null;
    }

    /// <summary>The upper-cased name that must be unique within a game.</summary>
    public static string Normalize(string displayName) => displayName.ToUpperInvariant();
}
