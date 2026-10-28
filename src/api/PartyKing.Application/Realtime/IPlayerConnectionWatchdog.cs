namespace PartyKing.Application.Realtime;

/// <summary>
/// After each host player report, arms a timer that pushes <c>PlayerStateChanged</c> with
/// <c>PlayerConnected=false</c> once the connection timeout elapses without a fresh report.
/// </summary>
public interface IPlayerConnectionWatchdog
{
    void Arm(Guid sessionId, int? songId, bool paused);

    void Disarm(Guid sessionId);
}
