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
    public void FellowSpies_WithBlindSpies_AreAnEmptyListForSpiesAndStillNullForTheRest()
    {
        var game = NewGame();
        game.Rules.BlindSpiesEnabled = true;

        Assert.Empty(View(game, 3).FellowSpies!);
        Assert.Empty(View(game, 4).FellowSpies!);
        Assert.Null(View(game, 0).FellowSpies);
        Assert.Null(View(game, null).FellowSpies);
        Assert.True(View(game, 3).Rules.BlindSpiesEnabled);
        Assert.False(View(NewGame(), 3).Rules.BlindSpiesEnabled);
    }

    [Fact]
    public void FellowSpies_WithAHiddenReverserSpy_LeaveThemOutOfTheRevealBothWays()
    {
        var game = NewGame();
        game.Rules.ReverserEnabled = true;
        Seat(game, 3).Character = ResistanceCharacter.Reverser;

        // By default the Reverser takes part in the reveal
        Assert.Equal([Ids[4]], View(game, 3).FellowSpies);
        Assert.Equal([Ids[3]], View(game, 4).FellowSpies);
        Assert.True(View(game, 3).Rules.ReverserSpyKnowsSpies);

        game.Rules.ReverserSpyKnowsSpies = false;

        Assert.Empty(View(game, 3).FellowSpies!);
        Assert.Empty(View(game, 4).FellowSpies!);
        Assert.Null(View(game, 0).FellowSpies);
        Assert.False(View(game, 3).Rules.ReverserSpyKnowsSpies);
    }

    [Fact]
    public void FellowSpies_WithTheSwitchOffButNoSpyReverser_AreUnchanged()
    {
        var game = NewGame();
        game.Rules.ReverserEnabled = true;
        game.Rules.ReverserSpyKnowsSpies = false;
        Seat(game, 1).Character = ResistanceCharacter.Reverser;

        Assert.Equal([Ids[4]], View(game, 3).FellowSpies);
        Assert.Equal([Ids[3]], View(game, 4).FellowSpies);
    }

    [Fact]
    public void YourCharacter_WithTheReverserModule_IsSeenOnlyByItsOwner()
    {
        var game = NewGame();
        game.Rules.ReverserEnabled = true;
        Seat(game, 1).Character = ResistanceCharacter.Reverser;
        Seat(game, 3).Character = ResistanceCharacter.Reverser;

        Assert.Equal(ResistanceCharacterDto.Reverser, View(game, 1).YourCharacter);
        Assert.Equal(ResistanceCharacterDto.Reverser, View(game, 3).YourCharacter);
        Assert.Equal(ResistanceCharacterDto.Standard, View(game, 0).YourCharacter);
        Assert.Equal(ResistanceCharacterDto.Standard, View(game, 4).YourCharacter);
        Assert.Null(View(game, null).YourCharacter);
        Assert.True(View(game, 0).Rules.ReverserEnabled);
    }

    [Fact]
    public void YourCharacter_WithTheDefectorModule_IsSeenOnlyByItsOwner_AndTheRulesArePublic()
    {
        var game = NewGame();
        game.Rules.DefectorEnabled = true;
        game.Rules.DefectorsKnowEachOther = true;
        Seat(game, 1).Character = ResistanceCharacter.Defector;
        Seat(game, 3).Character = ResistanceCharacter.Defector;

        Assert.Equal(ResistanceCharacterDto.Defector, View(game, 1).YourCharacter);
        Assert.Equal(ResistanceCharacterDto.Defector, View(game, 3).YourCharacter);
        Assert.Equal(ResistanceCharacterDto.Standard, View(game, 0).YourCharacter);
        Assert.Null(View(game, null).YourCharacter);
        foreach (var seat in new int?[] { 0, 3, null })
        {
            Assert.True(View(game, seat).Rules.DefectorEnabled);
            Assert.True(View(game, seat).Rules.DefectorsKnowEachOther);
        }

        Assert.False(View(NewGame(), 0).Rules.DefectorEnabled);
        Assert.False(View(NewGame(), 0).Rules.DefectorsKnowEachOther);
    }

    [Fact]
    public void FellowDefector_IsSeenOnlyByADefector_AndOnlyWithTheVariant()
    {
        var game = NewGame();
        game.Rules.DefectorEnabled = true;
        Seat(game, 1).Character = ResistanceCharacter.Defector;
        Seat(game, 3).Character = ResistanceCharacter.Defector;

        foreach (var seat in new int?[] { 0, 1, 3, 4, null })
        {
            Assert.Null(View(game, seat).FellowDefectorId);
        }

        game.Rules.DefectorsKnowEachOther = true;

        Assert.Equal(Ids[3], View(game, 1).FellowDefectorId);
        Assert.Equal(Ids[1], View(game, 3).FellowDefectorId);
        foreach (var seat in new int?[] { 0, 2, 4, null })
        {
            Assert.Null(View(game, seat).FellowDefectorId);
        }

        Assert.Null(View(NewGame(started: false), 1).FellowDefectorId);
    }

    [Fact]
    public void DefectorFlips_ArePublic_AndNeverShowTheCardsStillInTheDeck()
    {
        var game = NewGame();
        game.Rules.DefectorEnabled = true;
        game.DefectorDeck = "NCNCN";

        foreach (var seat in new int?[] { 0, 3, null })
        {
            Assert.Empty(View(game, seat).DefectorFlips);
        }

        game.MissionNumber = 4;

        foreach (var seat in new int?[] { 0, 3, null })
        {
            Assert.Equal([false, true], View(game, seat).DefectorFlips);
        }

        Assert.DoesNotContain("NCNCN", JsonSerializer.Serialize(View(game, 0)));
    }

    [Fact]
    public void DefectorFlips_WithTheModuleOff_AreEmpty()
    {
        var game = NewGame();
        game.DefectorDeck = "NCNCN";
        game.MissionNumber = 5;

        Assert.Empty(View(game, 0).DefectorFlips);
    }

    [Fact]
    public void ASwitchedDefector_ShowsTheirNewSide_ToThemselvesAndInTheEndReveal()
    {
        var game = NewGame(ResistancePhase.GameOver);
        game.Rules.DefectorEnabled = true;
        Seat(game, 2).Character = ResistanceCharacter.Defector;
        Seat(game, 3).Character = ResistanceCharacter.Defector;
        Seat(game, 2).Role = ResistanceRole.Spy;
        Seat(game, 3).Role = ResistanceRole.Resistance;
        game.DefectorDeck = "CNNNN";
        game.MissionNumber = 3;

        Assert.Equal(ResistanceRoleDto.Spy, View(game, 2).YourRole);
        Assert.Equal(ResistanceRoleDto.Resistance, View(game, 3).YourRole);
        var revealed = View(game, 0).RevealedRoles!;
        Assert.Equal(ResistanceRoleDto.Spy, revealed.Single(x => x.PlayerId == Ids[2]).Role);
        Assert.Equal(ResistanceCharacterDto.Defector, revealed.Single(x => x.PlayerId == Ids[2]).Character);
        Assert.Equal(ResistanceRoleDto.Resistance, revealed.Single(x => x.PlayerId == Ids[3]).Role);
    }

    [Fact]
    public void FellowSpies_WithDefectors_FollowTheReveal_AndTheSwitch()
    {
        var game = NewGame();
        game.Rules.DefectorEnabled = true;
        Seat(game, 1).Character = ResistanceCharacter.Defector;
        Seat(game, 4).Character = ResistanceCharacter.Defector;
        game.DefectorDeck = "CNNNN";

        // Before a switch: the spy Defector (seat 4) is known to the other spy but knows no spy
        Assert.Equal([Ids[4]], View(game, 3).FellowSpies);
        Assert.Empty(View(game, 4).FellowSpies!);
        Assert.Null(View(game, 1).FellowSpies);

        // After it: seat 4 is on the resistance side and seat 1 among the spies, whom nobody knows
        game.MissionNumber = 3;
        Seat(game, 4).Role = ResistanceRole.Resistance;
        Seat(game, 1).Role = ResistanceRole.Spy;

        Assert.Empty(View(game, 3).FellowSpies!);
        Assert.Empty(View(game, 1).FellowSpies!);
        Assert.Null(View(game, 4).FellowSpies);
    }

    [Theory]
    [InlineData(ResistanceReverserSides.Both, ResistanceReverserSidesDto.Both)]
    [InlineData(ResistanceReverserSides.Resistance, ResistanceReverserSidesDto.Resistance)]
    [InlineData(ResistanceReverserSides.Spy, ResistanceReverserSidesDto.Spy)]
    public void TheReverserSides_ArePartOfThePublicRules(ResistanceReverserSides sides, ResistanceReverserSidesDto expected)
    {
        var game = NewGame();
        game.Rules.ReverserEnabled = true;
        game.Rules.ReverserSides = sides;

        foreach (var seat in new int?[] { 0, 3, null })
        {
            Assert.Equal(expected, View(game, seat).Rules.ReverserSides);
        }
    }

    [Fact]
    public void YourCharacter_WithoutTheModuleOrBeforeTheStart_IsNull()
    {
        Assert.Null(View(NewGame(), 1).YourCharacter);
        Assert.False(View(NewGame(), 1).Rules.ReverserEnabled);

        var lobby = NewGame(started: false);
        lobby.Rules.ReverserEnabled = true;
        Assert.Null(View(lobby, 1).YourCharacter);
    }

    [Fact]
    public void RevealedRoles_AtTheEnd_ListTheCharactersToo()
    {
        var game = NewGame(ResistancePhase.GameOver);
        game.Rules.ReverserEnabled = true;
        Seat(game, 2).Character = ResistanceCharacter.Reverser;

        var revealed = View(game, 0).RevealedRoles!;

        Assert.Equal(ResistanceCharacterDto.Reverser, revealed.Single(x => x.PlayerId == Ids[2]).Character);
        Assert.Equal(1, revealed.Count(x => x.Character == ResistanceCharacterDto.Reverser));
    }

    [Fact]
    public void TheFailAndReverseCards_OfAFinishedMission_ArePublicButNotWhoPlayedThem()
    {
        var game = NewGame(ResistancePhase.MissionResult);
        game.Rules.ReverserEnabled = true;
        game.MissionResults.Add(new ResistanceMissionResult { MissionNumber = 1, Succeeded = true, FailCount = 1, ReverseCount = 1 });

        foreach (var seat in new int?[] { 0, 3, null })
        {
            var result = View(game, seat).MissionResults.Single();
            Assert.Equal((1, 1), (result.FailCount, result.ReverseCount));
        }
    }

    [Fact]
    public void YourMissionCard_ShowsAReverseCardOnlyToItsPlayer()
    {
        var game = NewGame(ResistancePhase.PlayingMission);
        Seat(game, 1).MissionCard = ResistanceMissionCardKind.Reverse;

        Assert.Equal(ResistanceMissionCardKindDto.Reverse, View(game, 1).YourMissionCard);
        foreach (var seat in new int?[] { 0, 2, 3, 4, null })
        {
            Assert.Null(View(game, seat).YourMissionCard);
        }

        Assert.Equal([Ids[1]], View(game, 0).PlayedMissionPlayerIds);
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
        Seat(game, 0).MissionCard = ResistanceMissionCardKind.Success;
        Seat(game, 3).MissionCard = ResistanceMissionCardKind.Fail;

        var resistance = View(game, 0);
        var spy = View(game, 3);
        var observer = View(game, null);

        Assert.Equal(ResistanceMissionCardKindDto.Success, resistance.YourMissionCard);
        Assert.Equal(ResistanceMissionCardKindDto.Fail, spy.YourMissionCard);
        Assert.Null(observer.YourMissionCard);
        Assert.Null(View(game, 1).YourMissionCard);
        Assert.Equal([Ids[0], Ids[3]], observer.PlayedMissionPlayerIds);
        Assert.Equal([Ids[0], Ids[3]], observer.TeamPlayerIds);
    }

    [Fact]
    public void ForcedOpenCard_IsPublicOnlyForTheCurrentMission()
    {
        var game = NewGame(ResistancePhase.PlayingMission);
        Seat(game, 3).MissionCard = ResistanceMissionCardKind.Fail;
        Card(game, 1, ResistancePlotCardType.InTheSpotlight, ResistancePlotCardLocation.Played, holder: 1, target: 3);

        var current = View(game, null);

        Assert.Equal(Ids[3], current.OpenMissionPlayerId);
        Assert.Equal(ResistanceMissionCardKindDto.Fail, current.OpenMissionCard);

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

    // ---- inquisitor ----

    /// <summary>The token started with seat 2 and passed to seat 4 (a spy), whom seat 2 has just picked.</summary>
    private static ResistanceGame InquisitionGame()
    {
        var game = NewGame(ResistancePhase.Inquisition);
        game.Rules.InquisitorEnabled = true;
        game.InquisitorPlayerId = Ids[4];
        game.InquisitionExaminerPlayerId = Ids[2];
        Seat(game, 2).WasInquisitor = true;
        Seat(game, 4).WasInquisitor = true;

        return game;
    }

    [Fact]
    public void Inquisition_ShowsTheLoyaltyOnlyToTheExaminer()
    {
        var game = InquisitionGame();

        var yours = View(game, 2).YourInquisition;

        Assert.NotNull(yours);
        Assert.Equal((Ids[4], ResistanceRoleDto.Spy), (yours.PlayerId, yours.Role));
        foreach (var seat in new int?[] { 0, 1, 3, 4, null })
        {
            Assert.Null(View(game, seat).YourInquisition);
        }
    }

    [Fact]
    public void Inquisition_TheLoyaltyIsGoneOnceTheExaminerConfirmed()
    {
        var game = InquisitionGame();
        game.InquisitionExaminerPlayerId = null;
        game.Phase = ResistancePhase.MissionResult;

        Assert.Null(View(game, 2).YourInquisition);
    }

    [Fact]
    public void Inquisition_BeforeAPlayerIsPicked_ShowsNothingPrivate()
    {
        var game = InquisitionGame();
        game.InquisitionExaminerPlayerId = null;
        game.InquisitorPlayerId = Ids[2];
        Seat(game, 4).WasInquisitor = false;

        for (var seat = 0; seat < Ids.Length; seat++)
        {
            Assert.Null(View(game, seat).YourInquisition);
        }
    }

    [Fact]
    public void Inquisition_TheTokenAndTheExaminationArePublic()
    {
        var view = View(InquisitionGame(), null);

        Assert.True(view.Rules.InquisitorEnabled);
        Assert.Equal(Ids[4], view.InquisitorPlayerId);
        Assert.Equal(Ids[2], view.InquisitionExaminerPlayerId);
        Assert.Equal(Ids[4], view.InquisitionExaminedPlayerId);
        Assert.Equal([Ids[2], Ids[4]], view.PastInquisitorPlayerIds);
        Assert.Equal(ResistancePhaseDto.Inquisition, view.Phase);
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
        if (phase != ResistancePhase.SelectingTeam)
        {
            // As in a real game, the result is recorded before the peek window opens
            game.MissionResults.Add(new ResistanceMissionResult { MissionNumber = 1, Succeeded = false, FailCount = 1 });
        }

        Seat(game, 3).MissionCard = ResistanceMissionCardKind.Fail;
        Card(game, 1, ResistancePlotCardType.KeepingCloseEye, ResistancePlotCardLocation.Played, holder: 1, target: 3, confirmed: true);

        var peeker = View(game, 1).YourPeeks;

        if (visible)
        {
            var peek = Assert.Single(peeker);
            Assert.Equal((Ids[3], ResistanceMissionCardKindDto.Fail), (peek.PlayerId, peek.Card));
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

    public static TheoryData<string> Scenarios => ["voting", "mission", "peek-window", "inquisition", "game-over", "lobby"];

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
                Seat(game, 0).MissionCard = ResistanceMissionCardKind.Success;
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
                Seat(game, 0).MissionCard = ResistanceMissionCardKind.Success;
                Seat(game, 3).MissionCard = ResistanceMissionCardKind.Fail;
                Seat(game, 1).PassedLeaderWindow = true;
                Card(game, 1, ResistancePlotCardType.KeepingCloseEye, ResistancePlotCardLocation.Played, holder: 1, target: 3, confirmed: true);
                Card(game, 2, ResistancePlotCardType.KeepingCloseEye, ResistancePlotCardLocation.Hand, holder: 2);

                return game;
            }
            case "inquisition":
            {
                var game = InquisitionGame();

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
            nameof(ResistanceGameDto.YourReveals), nameof(ResistanceGameDto.YourPeeks),
            nameof(ResistanceGameDto.YourInquisition)
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
