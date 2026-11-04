namespace PartyKing.API.Hubs;

/// <summary>
/// What a connection wants to hear about. Each topic is its own SignalR group per session, so a page receives only the
/// pushes of the topics it asked for (<c>?topics=music,ringo</c>).
/// </summary>
internal enum SessionHubTopic
{
    /// <summary>Queue, player state and commands, fallback playlist status and lyrics offsets.</summary>
    Music,
    Slideshow,
    Ringo,

    /// <summary>The Resistance game: every action of a round (<c>ResistanceChanged</c>, <c>ResistanceOpened</c>).</summary>
    Resistance,

    /// <summary>
    /// The Resistance lobby (exists, open, started, who joined): it changes a few times per game, not with every
    /// vote, so a tab that only shows the invite hears this and not <see cref="Resistance"/>.
    /// </summary>
    ResistanceLobby
}

internal static class SessionHubTopics
{
    public static readonly IReadOnlyList<SessionHubTopic> All = Enum.GetValues<SessionHubTopic>();

    /// <summary>
    /// Reads the comma-separated <c>topics</c> query value, case-insensitively. A missing value means every topic;
    /// <c>false</c> when the value names an unknown topic.
    /// </summary>
    public static bool TryParse(string? value, out IReadOnlyList<SessionHubTopic> topics)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            topics = All;
            return true;
        }

        var parsed = new List<SessionHubTopic>();

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<SessionHubTopic>(part, ignoreCase: true, out var topic) || !Enum.IsDefined(topic))
            {
                topics = [];
                return false;
            }

            if (!parsed.Contains(topic))
            {
                parsed.Add(topic);
            }
        }

        topics = parsed;
        return parsed.Count > 0;
    }
}
