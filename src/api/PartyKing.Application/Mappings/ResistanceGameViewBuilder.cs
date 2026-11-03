using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Application.Mappings;

/// <summary>
/// Builds the view of a Resistance game that one caller may see, one part at a time:
/// <c>ResistanceGameViewBuilder.For(game, you).WithLobby().WithRoles()...ToDto()</c>. Hidden information (roles,
/// votes, mission cards, plot cards, revealed identities) goes into the view only for the people it belongs to, see
/// the rules in CLAUDE.md; those properties of <see cref="ResistanceGameDto"/> start with "Your". A part that is
/// not added keeps the empty default, so a forgotten part can only hide information, never leak it.
/// </summary>
internal sealed class ResistanceGameViewBuilder
{
    private readonly ResistanceGame _game;
    private readonly ResistancePlayer? _you;
    private readonly IReadOnlyList<ResistancePlayer> _players;
    private readonly ResistanceRole? _yourRole;
    private readonly bool _votesRevealed;
    private readonly IReadOnlyCollection<Guid> _opinionMakers;
    private readonly Guid? _openMissionPlayerId;
    private readonly ResistanceGameDto _view;

    private ResistanceGameViewBuilder(ResistanceGame game, ResistancePlayer? you)
    {
        _game = game;
        _you = you;
        _players = game.Players.OrderBy(x => x.Order).ToList();
        _yourRole = game.IsStarted ? you?.Role : null;
        _votesRevealed = ResistanceRoles.VotesRevealed(game);
        _opinionMakers = ResistanceRoles.OpinionMakerIds(game);
        _openMissionPlayerId = ResistanceRoles.OpenMissionPlayerId(game);
        _view = new ResistanceGameDto { Rules = BuildRules(game) };
    }

    /// <summary>The whole view of the game for this viewer.</summary>
    public static ResistanceGameDto Build(ResistanceGame game, ResistancePlayer? you)
    {
        return For(game, you)
            .WithLobby()
            .WithRoles()
            .WithRound()
            .WithVotes()
            .WithMission()
            .WithPlotCards()
            .Build();
    }

    /// <param name="game">The game to show.</param>
    /// <param name="you">The viewer's own player, or null for someone who only watches.</param>
    private static ResistanceGameViewBuilder For(ResistanceGame game, ResistancePlayer? you)
    {
        return new ResistanceGameViewBuilder(game, you);
    }

    private ResistanceGameDto Build()
    {
        return _view;
    }

    /// <summary>Who is in the game and whether it has started.</summary>
    private ResistanceGameViewBuilder WithLobby()
    {
        _view.CreatedAt = _game.CreatedAt;
        _view.IsOpen = _game.IsOpen;
        _view.Players = _players.Select(x => new ResistancePlayerDto(x.PlayerId, x.DisplayName)).ToList();
        _view.YourPlayerId = _you?.PlayerId;
        _view.MaxPlayers = ResistanceGame.MaxPlayers;
        _view.StartedAt = _game.StartedAt;

        return this;
    }

    /// <summary>The viewer's own role, the spies' view of each other and, once the game is over, every role.</summary>
    private ResistanceGameViewBuilder WithRoles()
    {
        _view.YourRole = _yourRole?.ToDto();
        _view.FellowSpies = BuildFellowSpies();
        _view.RevealedRoles = BuildRevealedRoles();

        return this;
    }

    /// <summary>The leader, the team, the phase and how the game stands.</summary>
    private ResistanceGameViewBuilder WithRound()
    {
        _view.LeaderPlayerId = _game.LeaderPlayerId;
        _view.MissionNumber = _game.MissionNumber;
        _view.RequiredTeamSize = _game.IsStarted ? _game.Rules.TeamSize(_game.Players.Count, _game.MissionNumber) : null;
        _view.TeamPlayerIds = PlayerIds(x => x.IsOnTeam);
        _view.Phase = _game.IsStarted ? _game.Phase.ToDto() : null;
        _view.RejectedTeams = _game.RejectedTeams;
        _view.Winner = _game.Winner?.ToDto();

        return this;
    }

    /// <summary>Who voted on the team, and how once the votes are revealed.</summary>
    private ResistanceGameViewBuilder WithVotes()
    {
        _view.VotedPlayerIds = PlayerIds(x => x.Vote is not null);
        _view.YourVote = _you?.Vote;
        _view.Votes = _votesRevealed ? BuildVotes(_players) : null;
        _view.VoteApproved = _votesRevealed ? ResistanceRoles.IsApproved(_game) : null;

        return this;
    }

    /// <summary>The mission cards: who played one, the finished missions, the forced open card and what was peeked.</summary>
    private ResistanceGameViewBuilder WithMission()
    {
        _view.RequiredFails = _game.IsStarted ? _game.Rules.RequiredFails(_game.Players.Count, _game.MissionNumber) : null;
        _view.PlayedMissionPlayerIds = PlayerIds(x => x.MissionCard is not null);
        _view.YourMissionCard = _you?.MissionCard;
        _view.MissionResults = BuildMissionResults();
        _view.OpenMissionPlayerId = _openMissionPlayerId;
        _view.OpenMissionCard = _players.FirstOrDefault(x => x.PlayerId == _openMissionPlayerId)?.MissionCard;
        _view.YourPeeks = BuildYourPeeks();

        return this;
    }

    /// <summary>The plot cards add-on: the viewer's hand, the public counts and log, and the confirmation windows.</summary>
    private ResistanceGameViewBuilder WithPlotCards()
    {
        _view.YourPlotCards = BuildYourPlotCards();
        _view.PlotCardCounts = BuildPlotCardCounts();
        _view.OpinionMakerPlayerIds = PlayerIds(x => _opinionMakers.Contains(x.PlayerId));
        _view.OpinionMakerVotes = BuildOpinionMakerVotes();
        _view.PlayedPlotCards = BuildPlayedPlotCards();
        _view.YourReveals = BuildYourReveals();
        _view.LeaderWindowPassedPlayerIds = BuildWindowPassedPlayerIds();

        return this;
    }

    /// <summary>The ids of the players who match, in seat order.</summary>
    private List<Guid> PlayerIds(Func<ResistancePlayer, bool> predicate)
    {
        return _players.Where(predicate).Select(x => x.PlayerId).ToList();
    }

    /// <summary>The votes of the players who have voted, in the order given.</summary>
    private static List<ResistanceVoteDto> BuildVotes(IEnumerable<ResistancePlayer> players)
    {
        return players
            .Where(x => x.Vote.HasValue)
            .Select(x => new ResistanceVoteDto(x.PlayerId, x.Vote.GetValueOrDefault()))
            .ToList();
    }

    /// <summary>Only a spy sees the other spies.</summary>
    private List<Guid>? BuildFellowSpies()
    {
        if (_you is not { } you || _yourRole != ResistanceRole.Spy)
        {
            return null;
        }

        return PlayerIds(x => x.Role == ResistanceRole.Spy && x.PlayerId != you.PlayerId);
    }

    /// <summary>Every role, but only once the game is over.</summary>
    private List<ResistanceRevealedRoleDto>? BuildRevealedRoles()
    {
        if (_game.Phase != ResistancePhase.GameOver)
        {
            return null;
        }

        return _players.Select(x => new ResistanceRevealedRoleDto(x.PlayerId, x.Role!.Value.ToDto())).ToList();
    }

    private static ResistanceRulesDto BuildRules(ResistanceGame game)
    {
        var playerCounts = game.Rules.PlayerCounts
            .OrderBy(x => x.Key)
            .Select(x => new ResistanceCountRulesDto(x.Key, x.Value.Spies, x.Value.TeamSizes, x.Value.RequiredFails))
            .ToList();

        return new ResistanceRulesDto(playerCounts, game.Rules.PlotCardsEnabled, game.Rules.PlotCardsPerRound(game.Players.Count));
    }

    private List<ResistanceMissionResultDto> BuildMissionResults()
    {
        return _game.MissionResults
            .OrderBy(x => x.MissionNumber)
            .Select(x => new ResistanceMissionResultDto(x.MissionNumber, x.Succeeded, x.FailCount, x.TeamPlayerIds))
            .ToList();
    }

    /// <summary>Only the peeker sees the card they looked at, until the next mission starts.</summary>
    private List<ResistanceMissionPeekDto> BuildYourPeeks()
    {
        if (_you is not { } you)
        {
            return [];
        }

        return ResistanceRoles.PeekedCards(_game, you.PlayerId)
            .Select(x => new ResistanceMissionPeekDto(x.PlayerId, x.Success))
            .ToList();
    }

    /// <summary>The plot cards in the caller's hand (or still to be handed out), never anybody else's.</summary>
    private List<ResistancePlotCardDto> BuildYourPlotCards()
    {
        if (_you is not { } you)
        {
            return [];
        }

        return _game.PlotCards
            .Where(x => x.HolderPlayerId == you.PlayerId && x.Location != ResistancePlotCardLocation.Played)
            .OrderBy(x => x.CardId)
            .Select(x => new ResistancePlotCardDto(
                x.CardId,
                x.Type.ToDto(),
                ResistanceRoles.NeedsDealing(x),
                x.Location == ResistancePlotCardLocation.InPlay))
            .ToList();
    }

    /// <summary>The hand sizes are public, the cards are not.</summary>
    private List<ResistancePlotCardCountDto> BuildPlotCardCounts()
    {
        if (!_game.Rules.PlotCardsEnabled)
        {
            return [];
        }

        return _players
            .Select(x => new ResistancePlotCardCountDto(x.PlayerId, CountInHand(x)))
            .ToList();
    }

    private int CountInHand(ResistancePlayer player)
    {
        return _game.PlotCards.Count(x => x.HolderPlayerId == player.PlayerId && x.Location == ResistancePlotCardLocation.Hand);
    }

    /// <summary>The Opinion Makers' votes, once every one of them has voted (before the others may).</summary>
    private List<ResistanceVoteDto> BuildOpinionMakerVotes()
    {
        if (!ResistanceRoles.OpinionMakerVotesRevealed(_game))
        {
            return [];
        }

        return BuildVotes(_players.Where(x => _opinionMakers.Contains(x.PlayerId)));
    }

    /// <summary>The public log of played plot cards, newest first.</summary>
    private List<ResistancePlayedPlotCardDto> BuildPlayedPlotCards()
    {
        return _game.PlotCards
            .Where(x => x.Location == ResistancePlotCardLocation.Played)
            .OrderByDescending(x => x.PlayedAt)
            .Select(x => new ResistancePlayedPlotCardDto(
                x.CardId, x.Type.ToDto(), x.HolderPlayerId!.Value, x.TargetPlayerId,
                x.PlayedInMission, x.IsConfirmed))
            .ToList();
    }

    /// <summary>Only the player an identity card is shown to sees it, and only until they confirm.</summary>
    private List<ResistanceCardRevealDto> BuildYourReveals()
    {
        if (_you is not { } you)
        {
            return [];
        }

        var reveals = new List<ResistanceCardRevealDto>();
        foreach (var card in ResistanceRoles.UnconfirmedCards(_game).Where(x => ResistanceRoles.Viewer(x) == you.PlayerId))
        {
            var revealedId = ResistanceRoles.Revealed(card)!.Value;
            var role = _players.First(x => x.PlayerId == revealedId).Role!.Value;
            reveals.Add(new ResistanceCardRevealDto(card.CardId, revealedId, role.ToDto()));
        }

        return reveals;
    }

    /// <summary>Who confirmed in a window, never who holds a card.</summary>
    private List<Guid> BuildWindowPassedPlayerIds()
    {
        if (!ResistanceRoles.IsConfirmationWindow(_game))
        {
            return [];
        }

        return PlayerIds(x => x.PassedLeaderWindow);
    }
}
