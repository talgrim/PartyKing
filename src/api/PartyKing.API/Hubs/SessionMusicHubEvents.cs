namespace PartyKing.API.Hubs;

internal static class SessionMusicHubEvents
{
    public const string SongAdded = "SongAdded";
    public const string SongRemoved = "SongRemoved";
    public const string QueueAdvanced = "QueueAdvanced";
    public const string PlayerStateChanged = "PlayerStateChanged";
    public const string PlayerCommand = "PlayerCommand";
    public const string FallbackErrorChanged = "FallbackErrorChanged";
}
