using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>Moving a Resistance game from one step to the next: the vote, the windows, the mission and its result.</summary>
public static class ResistanceFlow
{
    /// <summary>A team is approved by a strict majority of all the players; a tie rejects it.</summary>
    public static bool IsApproved(ResistanceGame game)
    {
        return game.Players.Count(x => x.Vote == true) * 2 > game.Players.Count;
    }

    /// <summary>The player after the leader in seating order, wrapping around.</summary>
    public static Guid NextLeader(ResistanceGame game)
    {
        var ordered = game.Players.OrderBy(x => x.Order).ToList();
        var index = ordered.FindIndex(x => x.PlayerId == game.LeaderPlayerId);

        return ordered[(index + 1) % ordered.Count].PlayerId;
    }

    /// <summary>
    /// Applies the complete vote: an approval resets the rejection count and starts the mission, a rejection passes
    /// the lead on, and the fifth one ends the game.
    /// </summary>
    public static void ResolveVotes(ResistanceGame game)
    {
        if (IsApproved(game))
        {
            game.Publish(new ResistanceTeamApproved(game.SessionId, game.MissionNumber));
            if (ResistancePlotCardQueries.HasPlayableCards(game))
            {
                OpenWindow(game, ResistancePhase.ConfidenceWindow, beforeDraw: false);
                return;
            }

            StartMission(game);
            return;
        }

        RejectTeam(game);
    }

    private static void StartMission(ResistanceGame game)
    {
        game.RejectedTeams = 0;
        game.Phase = ResistancePhase.PlayingMission;
    }

    /// <summary>
    /// Counts the team as rejected (a vote, or a No Confidence cancelling the approval): the fifth ends the game, else
    /// the lead passes on and the next leader picks.
    /// </summary>
    public static void RejectTeam(ResistanceGame game)
    {
        game.RejectedTeams++;
        game.Publish(new ResistanceTeamRejected(game.SessionId, game.MissionNumber, game.RejectedTeams));
        if (game.RejectedTeams >= ResistanceRules.MaxRejectedTeams)
        {
            game.Phase = ResistancePhase.GameOver;
            game.Winner = ResistanceRole.Spy;
            game.Publish(new ResistanceGameOver(game.SessionId, ResistanceRole.Spy));
            return;
        }

        // The next leader picks at once; the votes stay (and are public) until that team is submitted
        game.ChangeLeader(NextLeader(game));
        game.Phase = ResistancePhase.SelectingTeam;
        foreach (var player in game.Players)
        {
            player.IsOnTeam = false;
        }

        if (ResistancePlotCardQueries.HasPlayableCards(game))
        {
            OpenWindow(game, ResistancePhase.LeaderWindow, beforeDraw: false);
        }
    }

    private static void OpenWindow(ResistanceGame game, ResistancePhase phase, bool beforeDraw)
    {
        game.Phase = phase;
        game.LeaderWindowBeforeDraw = beforeDraw;
        foreach (var player in game.Players)
        {
            player.PassedLeaderWindow = false;
        }
    }

    /// <summary>
    /// Ends a window (everyone passed, or a card was played). The leader window goes on to the draw or the team; the
    /// confidence window, passed by everyone, starts the mission, and the peek window shows the "Dalej" step.
    /// </summary>
    public static void AfterWindow(ResistanceGame game)
    {
        foreach (var player in game.Players)
        {
            player.PassedLeaderWindow = false;
        }

        if (game.Phase == ResistancePhase.ConfidenceWindow)
        {
            StartMission(game);
            return;
        }

        if (game.Phase == ResistancePhase.PeekWindow)
        {
            AfterMissionResolved(game);
            return;
        }

        if (game.LeaderWindowBeforeDraw)
        {
            DrawPlotCards(game);
            return;
        }

        game.Phase = ResistancePhase.SelectingTeam;
    }

    /// <summary>
    /// After a finished mission: the next player leads and picks again in the next mission, the team, the votes and the
    /// cards are cleared.
    /// </summary>
    public static void PassLeadership(ResistanceGame game)
    {
        game.MissionNumber++;
        game.ChangeLeader(NextLeader(game));
        foreach (var player in game.Players)
        {
            player.IsOnTeam = false;
            player.Vote = null;
            player.MissionCard = null;
        }

        BeginMission(game);
    }

    /// <summary>
    /// The leader of a new mission picks the team, after drawing and handing out the plot cards when the add-on is on.
    /// </summary>
    public static void BeginMission(ResistanceGame game)
    {
        game.Phase = ResistancePhase.SelectingTeam;
        ResistanceModules.MissionBegun(game);
        if (!game.Rules.PlotCardsEnabled)
        {
            return;
        }

        if (ResistancePlotCardQueries.HasPlayableCards(game))
        {
            OpenWindow(game, ResistancePhase.LeaderWindow, beforeDraw: true);
            return;
        }

        DrawPlotCards(game);
    }

    private static void DrawPlotCards(ResistanceGame game)
    {
        var toDraw = game.Rules.PlotCardsPerRound(game.Players.Count);
        foreach (var card in game.PlotCards
                     .Where(x => x.Location == ResistancePlotCardLocation.Deck)
                     .OrderBy(x => x.DeckOrder)
                     .Take(toDraw))
        {
            card.Location = ResistancePlotCardLocation.Drawn;
            card.HolderPlayerId = game.LeaderPlayerId;
        }

        game.Phase = ResistancePhase.DealingCards;
    }

    /// <summary>Whether the game is in a window in which every player confirms (see <see cref="ResistancePhases.IsConfirmationWindow"/>).</summary>
    public static bool IsConfirmationWindow(ResistanceGame game)
    {
        return ResistancePhases.IsConfirmationWindow(game.Phase);
    }

    /// <summary>
    /// Whether everybody's vote is public: all have voted and the vote is no longer on (also while the next leader
    /// picks after a rejection). While it is on, only who voted is known, not how.
    /// </summary>
    public static bool VotesRevealed(ResistanceGame game)
    {
        return game.Phase != ResistancePhase.Voting && game.Players.All(x => x.Vote is not null);
    }

    /// <summary>After the cards are dealt: plays the immediate ones first, otherwise the leader picks the team.</summary>
    public static void AfterDeal(ResistanceGame game)
    {
        game.Phase = ResistancePlotCardQueries.UnplayedImmediateCards(game).Any()
            ? ResistancePhase.ResolvingCards
            : ResistancePhase.SelectingTeam;
    }

    /// <summary>Whether every member of the approved team has played a card.</summary>
    public static bool AllCardsPlayed(ResistanceGame game)
    {
        return game.Players.Where(x => x.IsOnTeam).All(x => x.MissionCard is not null);
    }

    /// <summary>
    /// Applies the complete set of cards (see <see cref="ResistanceMissionRules"/>): the outcome and the number of
    /// Fail and Reverse cards are recorded (the played cards are shown to everybody without who played them, as when
    /// the cards are revealed shuffled at the table), and the third success or failure ends the game.
    /// </summary>
    public static void ResolveMission(ResistanceGame game)
    {
        var cards = game.Players.Where(x => x.IsOnTeam).Select(x => x.MissionCard!.Value).ToList();
        var fails = cards.Count(x => x == ResistanceMissionCardKind.Fail);
        var reverses = cards.Count(x => x == ResistanceMissionCardKind.Reverse);
        var succeeded = ResistanceMissionRules.HasSucceeded(cards, game.Rules.RequiredFails(game.Players.Count, game.MissionNumber));

        game.MissionResults.Add(new ResistanceMissionResult
        {
            SessionId = game.SessionId,
            MissionNumber = game.MissionNumber,
            FailCount = fails,
            ReverseCount = reverses,
            Succeeded = succeeded,
            TeamPlayerIds = game.Players.Where(x => x.IsOnTeam).OrderBy(x => x.Order).Select(x => x.PlayerId).ToList(),
        });

        game.Publish(new ResistanceMissionCompleted(game.SessionId, game.MissionNumber, succeeded, fails, reverses));

        // A module may have a phase of its own instead of the end (the Assassin names the Commander after the third
        // success). Nothing else happens in between (no peek window, no Inquisition), as after any mission that decides
        // the game.
        if (ResistanceModules.PhaseAfterScoring(game) is { } phase)
        {
            game.Phase = phase;
            return;
        }

        if (game.ResistanceScore >= ResistanceRules.MissionsToWin || game.SpyScore >= ResistanceRules.MissionsToWin)
        {
            game.Phase = ResistancePhase.GameOver;
            game.Winner = game.ResistanceScore >= ResistanceRules.MissionsToWin ? ResistanceRole.Resistance : ResistanceRole.Spy;
            game.Publish(new ResistanceGameOver(game.SessionId, game.Winner.Value));
            return;
        }

        // The outcome is public at once; a playable card in a hand opens the window for Keeping a Close Eye first
        if (ResistancePlotCardQueries.HasPlayableCards(game))
        {
            OpenWindow(game, ResistancePhase.PeekWindow, beforeDraw: false);
            return;
        }

        AfterMissionResolved(game);
    }

    /// <summary>
    /// The outcome of a mission that did not end the game is public and its windows are over: a module may have a phase
    /// of its own first (the Inquisitor examines after missions 2, 3 and 4); then the "Dalej" step.
    /// </summary>
    private static void AfterMissionResolved(ResistanceGame game)
    {
        game.Phase = ResistanceModules.PhaseAfterMissionResolved(game) ?? ResistancePhase.MissionResult;
    }

    /// <summary>
    /// Whether <paramref name="target"/> may be examined by <paramref name="examiner"/>: another player who never held
    /// the Inquisitor token.
    /// </summary>
    public static bool CanExamine(ResistanceGame game, Guid examiner, Guid target)
    {
        return target != examiner && game.Players.Any(x => x.PlayerId == target && !x.WasInquisitor);
    }
}
