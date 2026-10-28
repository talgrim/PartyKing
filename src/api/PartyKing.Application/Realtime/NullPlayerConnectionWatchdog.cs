namespace PartyKing.Application.Realtime;

internal sealed class NullPlayerConnectionWatchdog : IPlayerConnectionWatchdog
{
    public void Arm(Guid sessionId, int? songId, bool paused)
    {
    }

    public void Disarm(Guid sessionId)
    {
    }
}
