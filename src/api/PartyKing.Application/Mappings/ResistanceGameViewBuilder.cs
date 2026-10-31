using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Mappings;

/// <summary>
/// Builds the view of a Resistance game that one caller may see. Hidden information (roles, votes, mission cards,
/// plot cards, revealed identities) goes into the DTO only for the people it belongs to, see the rules in CLAUDE.md.
/// </summary>
internal static class ResistanceGameViewBuilder
{
    /// <param name="game">The game to show.</param>
    /// <param name="you">The viewer's own player, or null for someone who only watches.</param>
    public static ResistanceGameDto Build(ResistanceGame game, ResistancePlayer? you)
    {
        var players = game.Players
            .OrderBy(x => x.Order)
            .Select(x => new ResistancePlayerDto(x.PlayerId, x.DisplayName))
            .ToList();

        var yourRole = game.IsStarted ? you?.Role : null;

        // Hidden while the vote is on; public once all voted (also while the next leader picks after a rejection)
        var revealed = game.Phase != ResistancePhase.Voting && game.Players.All(x => x.Vote is not null);
        var voted = game.Players.Where(x => x.Vote is not null).OrderBy(x => x.Order).ToList();

        // Opinion Makers are public; their votes are too once every one of them has voted (before the others may)
        var opinionMakers = ResistanceRoles.OpinionMakerIds(game);

        return new ResistanceGameDto(
            game.CreatedAt,
            game.IsOpen,
            players,
            you?.PlayerId,
            ResistanceGame.MaxPlayers,
            game.StartedAt,
            game.LeaderPlayerId,
            yourRole is { } role ? role.ToDto() : null,
            FellowSpies(game, you, yourRole),
            game.MissionNumber,
            game.IsStarted ? game.Rules.TeamSize(game.Players.Count, game.MissionNumber) : null,
            game.Players.Where(x => x.IsOnTeam).OrderBy(x => x.Order).Select(x => x.PlayerId).ToList(),
            game.IsStarted ? game.Phase.ToDto() : null,
            game.RejectedTeams,
            game.Winner is { } winner ? winner.ToDto() : null,
            voted.Select(x => x.PlayerId).ToList(),
            you?.Vote,
            revealed ? voted.Select(x => new ResistanceVoteDto(x.PlayerId, x.Vote!.Value)).ToList() : null,
            revealed ? ResistanceRoles.IsApproved(game) : null,
            RevealedRoles(game),
            Rules(game),
            game.IsStarted ? game.Rules.RequiredFails(game.Players.Count, game.MissionNumber) : null,
            game.Players.Where(x => x.MissionCard is not null).OrderBy(x => x.Order).Select(x => x.PlayerId).ToList(),
            you?.MissionCard,
            game.MissionResults
                .OrderBy(x => x.MissionNumber)
                .Select(x => new ResistanceMissionResultDto(x.MissionNumber, x.Succeeded, x.FailCount, x.TeamPlayerIds))
                .ToList(),
            YourPlotCards(game, you),
            PlotCardCounts(game),
            game.Players.Where(x => opinionMakers.Contains(x.PlayerId)).OrderBy(x => x.Order).Select(x => x.PlayerId).ToList(),
            OpinionMakerVotes(game, opinionMakers),
            PlayedPlotCards(game),
            YourReveals(game, you),
            ResistanceRoles.OpenMissionPlayerId(game),
            game.Players.FirstOrDefault(x => x.PlayerId == ResistanceRoles.OpenMissionPlayerId(game))?.MissionCard,
            WindowPassedPlayerIds(game),
            YourPeeks(game, you));
    }

    /// <summary>Only a spy sees the other spies.</summary>
    private static List<Guid>? FellowSpies(ResistanceGame game, ResistancePlayer? you, ResistanceRole? yourRole) =>
        yourRole == ResistanceRole.Spy
            ? game.Players
                .Where(x => x.Role == ResistanceRole.Spy && x.PlayerId != you!.PlayerId)
                .OrderBy(x => x.Order)
                .Select(x => x.PlayerId)
                .ToList()
            : null;

    /// <summary>Every role, but only once the game is over.</summary>
    private static List<ResistanceRevealedRoleDto>? RevealedRoles(ResistanceGame game) =>
        game.Phase == ResistancePhase.GameOver
            ? game.Players
                .OrderBy(x => x.Order)
                .Select(x => new ResistanceRevealedRoleDto(x.PlayerId, x.Role!.Value.ToDto()))
                .ToList()
            : null;

    private static ResistanceRulesDto Rules(ResistanceGame game) =>
        new(
            game.Rules.PlayerCounts
                .OrderBy(x => x.Key)
                .Select(x => new ResistanceCountRulesDto(x.Key, x.Value.Spies, x.Value.TeamSizes, x.Value.RequiredFails))
                .ToList(),
            game.Rules.PlotCardsEnabled,
            game.Rules.PlotCardsPerRound(game.Players.Count));

    /// <summary>The plot cards in the caller's hand (or still to be handed out), never anybody else's.</summary>
    private static List<ResistancePlotCardDto> YourPlotCards(ResistanceGame game, ResistancePlayer? you) =>
        you is null
            ? []
            : game.PlotCards
                .Where(x => x.HolderPlayerId == you.PlayerId && x.Location != ResistancePlotCardLocation.Played)
                .OrderBy(x => x.CardId)
                .Select(x => new ResistancePlotCardDto(
                    x.CardId,
                    x.Type.ToDto(),
                    x.Location == ResistancePlotCardLocation.Drawn && x.Type != ResistancePlotCardType.EstablishConfidence,
                    x.Location == ResistancePlotCardLocation.InPlay))
                .ToList();

    /// <summary>The hand sizes are public, the cards are not.</summary>
    private static List<ResistancePlotCardCountDto> PlotCardCounts(ResistanceGame game) =>
        game.Rules.PlotCardsEnabled
            ? game.Players
                .OrderBy(x => x.Order)
                .Select(x => new ResistancePlotCardCountDto(
                    x.PlayerId,
                    game.PlotCards.Count(c => c.HolderPlayerId == x.PlayerId && c.Location == ResistancePlotCardLocation.Hand)))
                .ToList()
            : [];

    private static List<ResistanceVoteDto> OpinionMakerVotes(ResistanceGame game, IReadOnlyCollection<Guid> opinionMakers)
    {
        var revealed = opinionMakers.Count > 0
            && game.Players.Where(x => opinionMakers.Contains(x.PlayerId)).All(x => x.Vote is not null);

        return revealed
            ? game.Players
                .Where(x => opinionMakers.Contains(x.PlayerId))
                .OrderBy(x => x.Order)
                .Select(x => new ResistanceVoteDto(x.PlayerId, x.Vote!.Value))
                .ToList()
            : [];
    }

    /// <summary>The public log of played plot cards, newest first.</summary>
    private static List<ResistancePlayedPlotCardDto> PlayedPlotCards(ResistanceGame game) =>
        game.PlotCards
            .Where(x => x.Location == ResistancePlotCardLocation.Played)
            .OrderByDescending(x => x.PlayedAt)
            .Select(x => new ResistancePlayedPlotCardDto(
                x.CardId, x.Type.ToDto(), x.HolderPlayerId!.Value, x.TargetPlayerId,
                x.PlayedInMission, x.IsConfirmed))
            .ToList();

    /// <summary>Only the player an identity card is shown to sees it, and only until they confirm.</summary>
    private static List<ResistanceCardRevealDto> YourReveals(ResistanceGame game, ResistancePlayer? you) =>
        you is null
            ? []
            : ResistanceRoles.UnconfirmedCards(game)
                .Where(x => ResistanceRoles.Viewer(x) == you.PlayerId)
                .Select(x => new ResistanceCardRevealDto(
                    x.CardId,
                    ResistanceRoles.Revealed(x)!.Value,
                    game.Players.First(p => p.PlayerId == ResistanceRoles.Revealed(x)).Role!.Value.ToDto()))
                .ToList();

    /// <summary>Who confirmed in a window, never who holds a card.</summary>
    private static List<Guid> WindowPassedPlayerIds(ResistanceGame game) =>
        game.Phase is ResistancePhase.LeaderWindow or ResistancePhase.ConfidenceWindow or ResistancePhase.PeekWindow
            ? game.Players.Where(x => x.PassedLeaderWindow).OrderBy(x => x.Order).Select(x => x.PlayerId).ToList()
            : [];

    /// <summary>Only the peeker sees the card they looked at, until the next mission starts.</summary>
    private static List<ResistanceMissionPeekDto> YourPeeks(ResistanceGame game, ResistancePlayer? you) =>
        you is null
            ? []
            : ResistanceRoles.PeekedCards(game, you.PlayerId)
                .Select(x => new ResistanceMissionPeekDto(x.PlayerId, x.Success))
                .ToList();
}
