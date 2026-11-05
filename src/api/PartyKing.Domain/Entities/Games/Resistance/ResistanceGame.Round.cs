using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>The rounds of <see cref="ResistanceGame"/>: picking and voting on a team, the mission cards and moving on.</summary>
public partial class ResistanceGame
{
    /// <summary>
    /// The leader submits the team of the current mission, which locks it and puts it to the vote: exactly the number
    /// of different players the rules ask for.
    /// </summary>
    public ResistanceOutcome SelectTeam(Guid? callerPlayerId, IReadOnlyList<Guid> playerIds)
    {
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        if (callerPlayerId is null || callerPlayerId != LeaderPlayerId)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotLeaderToPick, this);
        }

        if (Phase is ResistancePhase.DealingCards or ResistancePhase.ResolvingCards)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardsNotDealt, this);
        }

        if (Phase is ResistancePhase.LeaderWindow or ResistancePhase.VoteResult)
        {
            // No team has been picked in these phases, so "a team was already submitted" would be wrong
            return ResistanceOutcome.Failed(ResistanceFailure.NotSelectingTeam, this);
        }

        if (Phase != ResistancePhase.SelectingTeam)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.TeamSubmitted, this);
        }

        if (playerIds.Count != Rules.TeamSize(Players.Count, MissionNumber)
            || playerIds.Distinct().Count() != playerIds.Count)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.WrongTeamSize, this);
        }

        if (playerIds.Any(id => Players.All(x => x.PlayerId != id)))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.UnknownTeamPlayer, this);
        }

        foreach (var player in Players)
        {
            player.IsOnTeam = playerIds.Contains(player.PlayerId);
            player.Vote = null;
            player.MissionCard = null;
        }

        Phase = ResistancePhase.Voting;
        Raise(new ResistanceTeamSubmitted(SessionId, MissionNumber, LeaderPlayerId!.Value, playerIds.ToList()));

        return Succeeded();
    }

    /// <summary>
    /// A player votes on the submitted team (a repeated vote replaces the earlier one). The vote that completes the
    /// round is tallied right away: a majority approves, otherwise (a tie too) the team is rejected, which counts
    /// towards the five that make the spies win.
    /// </summary>
    public ResistanceOutcome Vote(Guid? voterId, bool approve)
    {
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        var voter = Players.FirstOrDefault(x => x.PlayerId == voterId);
        if (voter is null)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotPlayerToVote, this);
        }

        if (Phase != ResistancePhase.Voting)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotVoting, this);
        }

        var opinionMakers = ResistanceRoles.OpinionMakerIds(this);
        if (opinionMakers.Contains(voter.PlayerId))
        {
            if (voter.Vote is not null)
            {
                return ResistanceOutcome.Failed(ResistanceFailure.VoteLocked, this);
            }
        }
        else if (opinionMakers.Any(id => Players.First(x => x.PlayerId == id).Vote is null))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.OpinionMakersFirst, this);
        }

        voter.Vote = approve;
        Raise(new ResistanceVoteCast(SessionId, voter.PlayerId));
        if (Players.All(x => x.Vote is not null))
        {
            ResistanceRoles.ResolveVotes(this);
        }

        return Succeeded();
    }

    /// <summary>
    /// A member of the approved team secretly plays a Success, Fail or (a Reverser only) Reverse card; it can't be
    /// changed. The card that completes the team is resolved right away (see <see cref="ResistanceMissionRules"/>),
    /// and the third success or failure ends the game.
    /// </summary>
    public ResistanceOutcome PlayMissionCard(Guid? callerPlayerId, ResistanceMissionCardKind card)
    {
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        var player = Players.FirstOrDefault(x => x.PlayerId == callerPlayerId);
        if (player is null)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotPlayerToPlay, this);
        }

        if (Phase != ResistancePhase.PlayingMission)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NoApprovedTeam, this);
        }

        if (!player.IsOnTeam)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotOnTeam, this);
        }

        if (player.MissionCard is not null)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.AlreadyPlayed, this);
        }

        if (card == ResistanceMissionCardKind.Fail && player.Role == ResistanceRole.Resistance)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.MustSucceed, this);
        }

        if (ReverserCardFailure(player, card) is { } reverserFailure)
        {
            return ResistanceOutcome.Failed(reverserFailure, this);
        }

        player.MissionCard = card;
        Raise(new ResistanceMissionCardPlayed(SessionId, player.PlayerId));
        if (ResistanceRoles.AllCardsPlayed(this))
        {
            ResistanceRoles.ResolveMission(this);
        }

        return Succeeded();
    }

    /// <summary>
    /// Moves on after a finished mission (only valid in <see cref="ResistancePhase.MissionResult"/>): the next player
    /// leads, the team, the votes and the cards are cleared and the next mission starts. A rejected team needs no
    /// "continue", the lead passes at once. The leader or a manager may do it.
    /// </summary>
    public ResistanceOutcome Continue(Guid? callerPlayerId, bool isManager)
    {
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        if (!isManager && (callerPlayerId is null || callerPlayerId != LeaderPlayerId))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotAllowedToContinue, this);
        }

        if (Phase != ResistancePhase.MissionResult)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NothingToContinue, this);
        }

        ResistanceRoles.PassLeadership(this);

        return Succeeded();
    }
}
