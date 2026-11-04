using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>
/// Taking a seat over: a player whose device is gone (a closed private tab, a lost cookie) can't come back on their
/// own, so the host or an admin releases the seat and a guest claims it, which gives the seat a new
/// <see cref="ResistancePlayer.ClaimStamp"/> and so cuts the old device off.
/// </summary>
public partial class ResistanceGame
{
    /// <summary>
    /// The host or an admin releases the seat of an offline player of a started game for a guest to take over, or takes
    /// the release back (which is allowed whether the player is online or not). The seat of the host and of an admin
    /// is never released. <paramref name="playerOnline"/> is whether a device of the player is connected now.
    /// </summary>
    public ResistanceOutcome SetSeatReleased(Guid playerId, bool released, bool playerOnline)
    {
        if (!IsStarted)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotStarted, this);
        }

        var player = Players.FirstOrDefault(x => x.PlayerId == playerId);
        if (player is null)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.UnknownSeat, this);
        }

        if (released && player.IsManagerSeat)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.ManagerSeat, this);
        }

        if (released && playerOnline)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.SeatOnline, this);
        }

        player.IsReclaimable = released;
        Raise(new ResistanceSeatReleaseChanged(SessionId, playerId, released));

        return Succeeded();
    }

    /// <summary>
    /// A guest takes over a released seat: the release ends and the seat gets <paramref name="newStamp"/>, which the
    /// guest's cookie carries from now on. A seat whose player is connected again can't be taken over (the release
    /// stays, so it is free again when they drop). Whoever claims it, an unknown seat and a seat that was not released
    /// are refused alike.
    /// </summary>
    public ResistanceOutcome ClaimSeat(Guid playerId, bool playerOnline, Guid newStamp)
    {
        if (!IsStarted)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotStarted, this);
        }

        var player = Players.FirstOrDefault(x => x.PlayerId == playerId);
        if (player is null || !player.IsReclaimable || player.IsManagerSeat)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.SeatNotReleased, this);
        }

        if (playerOnline)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.SeatOnline, this);
        }

        player.IsReclaimable = false;
        player.ClaimStamp = newStamp;
        Raise(new ResistanceSeatClaimed(SessionId, playerId));

        return Succeeded();
    }
}
