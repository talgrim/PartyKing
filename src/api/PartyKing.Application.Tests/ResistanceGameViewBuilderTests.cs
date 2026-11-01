using System.Text.Json;
using PartyKing.Application.Mappings;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Application.Tests;

/// <summary>
/// Pins what each kind of viewer may see of a Resistance game: hidden information (roles, votes, mission cards, plot
/// cards, identity reveals, peeks) goes only to the people it belongs to, everything else is the same for everybody.
/// </summary>
public class ResistanceGameViewBuilderTests
{
    // Seats 0-2 are the resistance, seats 3-4 the spies.
    private static readonly Guid[] Ids = Enumerable.Range(1, 5).Select(i => new Guid(i, 0, 0, new byte[8])).ToArray();

    private static ResistanceGame NewGame(ResistancePhase phase = ResistancePhase.SelectingTeam, bool started = true)
    {
        var game = new ResistanceGame
        {
            CreatedAt = DateTimeOffset.UnixEpoch,
            IsOpen = true,
            StartedAt = started ? DateTimeOffset.UnixEpoch : null,
            LeaderPlayerId = Ids[0],
            Phase = phase,
            Rules = ResistanceRules.Default()
        };
        game.Rules.PlotCardsEnabled = true;
        for (var i = 0; i < Ids.Length; i++)
        {
            game.Players.Add(new ResistancePlayer
            {
                PlayerId = Ids[i],
                Name = $"P{i}",
                Order = i,
                Role = i < 3 ? ResistanceRole.Resistance : ResistanceRole.Spy
            });
        }

        return game;
    }

    private static ResistancePlayer Seat(ResistanceGame game, int seat) => game.Players.Single(x => x.PlayerId == Ids[seat]);

    private static ResistancePlotCard Card(
        ResistanceGame game,
        int id,
        ResistancePlotCardType type,
        ResistancePlotCardLocation location,
        int? holder,
        int? target = null,
        int playedInMission = 1,
        bool confirmed = false,
        int playedAtMinute = 0)
    {
        var card = new ResistancePlotCard
        {
            CardId = id,
            Type = type,
            Location = location,
            HolderPlayerId = holder is null ? null : Ids[holder.Value],
            TargetPlayerId = target is null ? null : Ids[target.Value],
            PlayedInMission = playedInMission,
            IsConfirmed = confirmed,
            PlayedAt = location == ResistancePlotCardLocation.Played ? DateTimeOffset.UnixEpoch.AddMinutes(playedAtMinute) : null
        };
        game.PlotCards.Add(card);

        return card;
    }

    /// <summary>The view of the player in <paramref name="seat"/>, or of an observer when it is null.</summary>
    private static ResistanceGameDto View(ResistanceGame game, int? seat) =>
        ResistanceGameViewBuilder.Build(game, seat is null ? null : Seat(game, seat.Value));

    // ---- roles ----

    [Fact]
    public void Role_IsSeenOnlyByItsOwner()
    {
        var game = NewGame();

        Assert.Equal(ResistanceRoleDto.Resistance, View(game, 1).YourRole);
        Assert.Equal(ResistanceRoleDto.Spy, View(game, 3).YourRole);
        Assert.Null(View(game, null).YourRole);
    }

    [Fact]
    public void FellowSpies_AreSeenOnlyBySpies_AndNeverIncludeThemselves()
    {
        var game = NewGame();

        Assert.Equal([Ids[4]], View(game, 3).FellowSpies);
        Assert.Equal([Ids[3]], View(game, 4).FellowSpies);
        Assert.Null(View(game, 0).FellowSpies);
        Assert.Null(View(game, null).FellowSpies);
    }

    [Fact]
    public void RoleAndSpies_AreHiddenBeforeTheStart()
    {
        var game = NewGame(started: false);

        var spy = View(game, 3);

        Assert.Null(spy.YourRole);
        Assert.Null(spy.FellowSpies);
        Assert.Equal(Ids[3], spy.YourPlayerId);
        Assert.Null(spy.Phase);
        Assert.Null(spy.RequiredTeamSize);
        Assert.Null(spy.RequiredFails);
    }

    [Fact]
    public void RevealedRoles_AppearOnlyWhenTheGameIsOver()
    {
        var running = NewGame(ResistancePhase.PlayingMission);
        var over = NewGame(ResistancePhase.GameOver);

        Assert.All(new[] { 0, 3, (int?)null }, seat => Assert.Null(View(running, seat).RevealedRoles));

        var revealed = View(over, null).RevealedRoles!;
        Assert.Equal(5, revealed.Count);
        Assert.Equal(ResistanceRoleDto.Spy, revealed.Single(x => x.PlayerId == Ids[4]).Role);
        Assert.Equal(ResistanceRoleDto.Resistance, revealed.Single(x => x.PlayerId == Ids[0]).Role);
    }

    // ---- votes ----

    [Fact]
    public void WhileVoting_OnlyWhoVotedIsPublic_AndYourVoteIsYours()
    {
        var game = NewGame(ResistancePhase.Voting);
        Seat(game, 0).Vote = true;
        Seat(game, 2).Vote = false;

        var voter = View(game, 0);
        var other = View(game, 1);

        Assert.Equal([Ids[0], Ids[2]], other.VotedPlayerIds);
        Assert.Equal(true, voter.YourVote);
        Assert.Null(other.YourVote);
        Assert.Null(View(game, null).YourVote);
        Assert.Null(other.Votes);
        Assert.Null(other.VoteApproved);
    }

    [Fact]
    public void WhenEveryoneVotedButTheVoteIsStillOn_VotesStayHidden()
    {
        var game = NewGame(ResistancePhase.Voting);
        foreach (var player in game.Players)
        {
            player.Vote = true;
        }

        var view = View(game, null);

        Assert.Equal(5, view.VotedPlayerIds.Count);
        Assert.Null(view.Votes);
        Assert.Null(view.VoteApproved);
    }

    [Fact]
    public void WhenEveryoneVotedAndTheVoteIsOver_VotesAreOpenToAll()
    {
        var game = NewGame(ResistancePhase.SelectingTeam);
        for (var i = 0; i < 5; i++)
        {
            game.Players[i].Vote = i < 2; // 2 for, 3 against: rejected
        }

        var view = View(game, null);

        Assert.Equal(5, view.Votes!.Count);
        Assert.Equal([true, true, false, false, false], view.Votes.Select(x => x.Approve));
        Assert.False(view.VoteApproved);
    }

    [Fact]
    public void WhenOnlySomeVotedAndTheVoteIsOver_VotesStayHidden()
    {
        var game = NewGame(ResistancePhase.SelectingTeam);
        Seat(game, 0).Vote = true;

        var view = View(game, null);

        Assert.Null(view.Votes);
        Assert.Null(view.VoteApproved);
    }

    // ---- missions ----

    [Fact]
    public void MissionCards_AreSecret_OnlyWhoPlayedIsPublic()
    {
        var game = NewGame(ResistancePhase.PlayingMission);
        Seat(game, 0).IsOnTeam = true;
        Seat(game, 3).IsOnTeam = true;
        Seat(game, 0).MissionCard = true;
        Seat(game, 3).MissionCard = false;

        var resistance = View(game, 0);
        var spy = View(game, 3);
        var observer = View(game, null);

        Assert.Equal(true, resistance.YourMissionCard);
        Assert.Equal(false, spy.YourMissionCard);
        Assert.Null(observer.YourMissionCard);
        Assert.Null(View(game, 1).YourMissionCard);
        Assert.Equal([Ids[0], Ids[3]], observer.PlayedMissionPlayerIds);
        Assert.Equal([Ids[0], Ids[3]], observer.TeamPlayerIds);
    }

    [Fact]
    public void ForcedOpenCard_IsPublicOnlyForTheCurrentMission()
    {
        var game = NewGame(ResistancePhase.PlayingMission);
        Seat(game, 3).MissionCard = false;
        Card(game, 1, ResistancePlotCardType.InTheSpotlight, ResistancePlotCardLocation.Played, holder: 1, target: 3);

        var current = View(game, null);

        Assert.Equal(Ids[3], current.OpenMissionPlayerId);
        Assert.Equal(false, current.OpenMissionCard);

        game.MissionNumber = 2;
        var next = View(game, null);

        Assert.Null(next.OpenMissionPlayerId);
        Assert.Null(next.OpenMissionCard);
    }

    // ---- plot cards ----

    [Fact]
    public void YourPlotCards_AreOnlyYourOwnUnplayedCards()
    {
        var game = NewGame();
        Card(game, 1, ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Hand, holder: 1);
        Card(game, 2, ResistancePlotCardType.StrongLeader, ResistancePlotCardLocation.Hand, holder: 2);
        Card(game, 3, ResistancePlotCardType.OpenUp, ResistancePlotCardLocation.Played, holder: 1, target: 2);
        Card(game, 4, ResistancePlotCardType.OpinionMaker, ResistancePlotCardLocation.InPlay, holder: 1);

        var mine = View(game, 1).YourPlotCards;

        Assert.Equal([1, 4], mine.Select(x => x.Id));
        Assert.False(mine.Single(x => x.Id == 1).InPlay);
        Assert.True(mine.Single(x => x.Id == 4).InPlay);
        Assert.Equal([2], View(game, 2).YourPlotCards.Select(x => x.Id));
        Assert.Empty(View(game, 0).YourPlotCards);
        Assert.Empty(View(game, null).YourPlotCards);
    }

    [Fact]
    public void DrawnCards_AreMarkedToDealExceptEstablishConfidence()
    {
        var game = NewGame(ResistancePhase.DealingCards);
        Card(game, 1, ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Drawn, holder: 0);
        Card(game, 2, ResistancePlotCardType.EstablishConfidence, ResistancePlotCardLocation.Drawn, holder: 0);

        var leader = View(game, 0).YourPlotCards;

        Assert.True(leader.Single(x => x.Id == 1).ToDeal);
        Assert.False(leader.Single(x => x.Id == 2).ToDeal);
    }

    [Fact]
    public void PlotCardCounts_AreCountsOfHandCardsOnly_AndPublic()
    {
        var game = NewGame();
        Card(game, 1, ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Hand, holder: 1);
        Card(game, 2, ResistancePlotCardType.StrongLeader, ResistancePlotCardLocation.Hand, holder: 1);
        Card(game, 3, ResistancePlotCardType.OpenUp, ResistancePlotCardLocation.Played, holder: 1, target: 2);
        Card(game, 4, ResistancePlotCardType.OpinionMaker, ResistancePlotCardLocation.InPlay, holder: 2);

        var counts = View(game, null).PlotCardCounts;

        Assert.Equal(5, counts.Count);
        Assert.Equal(2, counts.Single(x => x.PlayerId == Ids[1]).Count);
        Assert.Equal(0, counts.Single(x => x.PlayerId == Ids[2]).Count);
    }

    [Fact]
    public void PlotCardCounts_AreEmptyWhenTheAddOnIsOff()
    {
        var game = NewGame();
        game.Rules.PlotCardsEnabled = false;

        Assert.Empty(View(game, null).PlotCardCounts);
    }

    [Fact]
    public void OpinionMakers_ArePublic_AndTheirVotesAppearOnceAllOfThemVoted()
    {
        var game = NewGame(ResistancePhase.Voting);
        Card(game, 1, ResistancePlotCardType.OpinionMaker, ResistancePlotCardLocation.InPlay, holder: 1);
        Card(game, 2, ResistancePlotCardType.OpinionMaker, ResistancePlotCardLocation.InPlay, holder: 3);
        Seat(game, 1).Vote = true;

        var halfWay = View(game, null);

        Assert.Equal([Ids[1], Ids[3]], halfWay.OpinionMakerPlayerIds);
        Assert.Empty(halfWay.OpinionMakerVotes);

        Seat(game, 3).Vote = false;
        var done = View(game, null);

        Assert.Equal([(Ids[1], true), (Ids[3], false)], done.OpinionMakerVotes.Select(x => (x.PlayerId, x.Approve)));
        Assert.Null(done.Votes);
    }

    [Fact]
    public void PlayedPlotCards_AreAPublicLog_NewestFirst()
    {
        var game = NewGame();
        Card(game, 1, ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Played, holder: 1, target: 0, confirmed: true, playedAtMinute: 1);
        Card(game, 2, ResistancePlotCardType.StrongLeader, ResistancePlotCardLocation.Played, holder: 2, target: 0, confirmed: true, playedAtMinute: 5);
        Card(game, 3, ResistancePlotCardType.OpenUp, ResistancePlotCardLocation.Hand, holder: 2);

        var log = View(game, null).PlayedPlotCards;

        Assert.Equal([2, 1], log.Select(x => x.Id));
        Assert.Equal(Ids[2], log[0].HolderPlayerId);
        Assert.Equal(Ids[0], log[0].TargetPlayerId);
    }

    [Fact]
    public void Reveals_AreSeenOnlyByTheViewerUntilTheyConfirm()
    {
        var game = NewGame(ResistancePhase.ResolvingCards);
        // The holder of Overheard Conversation sees the neighbour's identity; for the others the target does.
        Card(game, 1, ResistancePlotCardType.OverheardConversation, ResistancePlotCardLocation.Played, holder: 1, target: 3);
        Card(game, 2, ResistancePlotCardType.OpenUp, ResistancePlotCardLocation.Played, holder: 4, target: 2);

        var overheardViewer = View(game, 1).YourReveals;
        var openUpViewer = View(game, 2).YourReveals;

        var overheard = Assert.Single(overheardViewer);
        Assert.Equal((1, Ids[3], ResistanceRoleDto.Spy), (overheard.CardId, overheard.PlayerId, overheard.Role));
        var openUp = Assert.Single(openUpViewer);
        Assert.Equal((2, Ids[4], ResistanceRoleDto.Spy), (openUp.CardId, openUp.PlayerId, openUp.Role));

        foreach (var seat in new int?[] { 0, 3, 4, null })
        {
            Assert.Empty(View(game, seat).YourReveals);
        }

        game.PlotCards.Single(x => x.CardId == 1).IsConfirmed = true;

        Assert.Empty(View(game, 1).YourReveals);
        Assert.Single(View(game, 2).YourReveals);
    }

    // ---- windows ----

    [Theory]
    [InlineData(ResistancePhase.LeaderWindow, true)]
    [InlineData(ResistancePhase.ConfidenceWindow, true)]
    [InlineData(ResistancePhase.PeekWindow, true)]
    [InlineData(ResistancePhase.SelectingTeam, false)]
    [InlineData(ResistancePhase.Voting, false)]
    [InlineData(ResistancePhase.PlayingMission, false)]
    [InlineData(ResistancePhase.MissionResult, false)]
    [InlineData(ResistancePhase.ResolvingCards, false)]
    public void WhoConfirmedAWindow_IsPublicOnlyInsideAWindow(ResistancePhase phase, bool listed)
    {
        var game = NewGame(phase);
        Seat(game, 1).PassedLeaderWindow = true;
        Seat(game, 4).PassedLeaderWindow = true;

        var passed = View(game, null).LeaderWindowPassedPlayerIds;

        Assert.Equal(listed ? [Ids[1], Ids[4]] : [], passed);
    }

    // ---- peeks ----

    [Theory]
    [InlineData(ResistancePhase.PeekWindow, true)]
    [InlineData(ResistancePhase.MissionResult, true)]
    [InlineData(ResistancePhase.SelectingTeam, false)]
    [InlineData(ResistancePhase.GameOver, false)]
    public void Peeks_AreSeenOnlyByThePeeker_AndOnlyAfterTheMission(ResistancePhase phase, bool visible)
    {
        var game = NewGame(phase);
        Seat(game, 3).MissionCard = false;
        Card(game, 1, ResistancePlotCardType.KeepingCloseEye, ResistancePlotCardLocation.Played, holder: 1, target: 3, confirmed: true);

        var peeker = View(game, 1).YourPeeks;

        if (visible)
        {
            var peek = Assert.Single(peeker);
            Assert.Equal((Ids[3], false), (peek.PlayerId, peek.Success));
        }
        else
        {
            Assert.Empty(peeker);
        }

        foreach (var seat in new int?[] { 0, 2, 3, 4, null })
        {
            Assert.Empty(View(game, seat).YourPeeks);
        }
    }

    // ---- the public part is the same for everybody ----

    public static TheoryData<string> Scenarios => ["voting", "mission", "peek-window", "game-over", "lobby"];

    private static ResistanceGame Scenario(string name)
    {
        switch (name)
        {
            case "voting":
            {
                var game = NewGame(ResistancePhase.Voting);
                Seat(game, 0).Vote = true;
                Seat(game, 1).Vote = false;
                Seat(game, 3).Vote = true;
                Card(game, 1, ResistancePlotCardType.OpinionMaker, ResistancePlotCardLocation.InPlay, holder: 3);
                Card(game, 2, ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Hand, holder: 2);
                Card(game, 3, ResistancePlotCardType.StrongLeader, ResistancePlotCardLocation.Hand, holder: 4);
                Card(game, 4, ResistancePlotCardType.OverheardConversation, ResistancePlotCardLocation.Played, holder: 1, target: 0);
                Card(game, 5, ResistancePlotCardType.OpenUp, ResistancePlotCardLocation.Played, holder: 4, target: 1, playedAtMinute: 3);
                Card(game, 6, ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Drawn, holder: 0);

                return game;
            }
            case "mission":
            {
                var game = NewGame(ResistancePhase.PlayingMission);
                foreach (var player in game.Players)
                {
                    player.Vote = true;
                }

                Seat(game, 0).IsOnTeam = true;
                Seat(game, 3).IsOnTeam = true;
                Seat(game, 0).MissionCard = true;
                Card(game, 1, ResistancePlotCardType.InTheSpotlight, ResistancePlotCardLocation.Played, holder: 1, target: 3, confirmed: true);

                return game;
            }
            case "peek-window":
            {
                var game = NewGame(ResistancePhase.PeekWindow);
                game.MissionResults.Add(new ResistanceMissionResult
                {
                    MissionNumber = 1,
                    Succeeded = false,
                    FailCount = 1,
                    TeamPlayerIds = [Ids[0], Ids[3]]
                });
                Seat(game, 0).IsOnTeam = true;
                Seat(game, 3).IsOnTeam = true;
                Seat(game, 0).MissionCard = true;
                Seat(game, 3).MissionCard = false;
                Seat(game, 1).PassedLeaderWindow = true;
                Card(game, 1, ResistancePlotCardType.KeepingCloseEye, ResistancePlotCardLocation.Played, holder: 1, target: 3, confirmed: true);
                Card(game, 2, ResistancePlotCardType.KeepingCloseEye, ResistancePlotCardLocation.Hand, holder: 2);

                return game;
            }
            case "game-over":
            {
                var game = NewGame(ResistancePhase.GameOver);
                game.Winner = ResistanceRole.Spy;
                game.RejectedTeams = 5;

                return game;
            }
            default:
                return NewGame(started: false);
        }
    }

    /// <summary>The caller-only fields blanked: what is left is what every viewer is allowed to see.</summary>
    private static string PublicPart(ResistanceGameDto dto)
    {
        var json = JsonSerializer.SerializeToNode(dto)!.AsObject();
        string[] callerOnly =
        [
            nameof(ResistanceGameDto.YourPlayerId), nameof(ResistanceGameDto.YourRole),
            nameof(ResistanceGameDto.FellowSpies), nameof(ResistanceGameDto.YourVote),
            nameof(ResistanceGameDto.YourMissionCard), nameof(ResistanceGameDto.YourPlotCards),
            nameof(ResistanceGameDto.YourReveals), nameof(ResistanceGameDto.YourPeeks)
        ];

        foreach (var name in callerOnly)
        {
            Assert.True(json.Remove(name), $"{name} is not a property of the serialized DTO");
        }

        return json.ToJsonString();
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void PublicPart_IsIdenticalForEveryViewer(string scenario)
    {
        var game = Scenario(scenario);
        var expected = PublicPart(View(game, null));

        for (var seat = 0; seat < Ids.Length; seat++)
        {
            Assert.Equal(expected, PublicPart(View(game, seat)));
        }
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void EveryViewer_SeesOnlyOwnPlayerId(string scenario)
    {
        var game = Scenario(scenario);

        Assert.Null(View(game, null).YourPlayerId);
        for (var seat = 0; seat < Ids.Length; seat++)
        {
            Assert.Equal(Ids[seat], View(game, seat).YourPlayerId);
        }
    }
}
