using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Results;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

internal class ResistanceGamesRepository
    : LobbyGamesRepository<ResistanceGame, ResistancePlayer>, IResistanceGamesRepository
{
    public ResistanceGamesRepository(
        IDbContextFactory<ReaderDbContext> readerFactory,
        IDbContextFactory<WriterDbContext> writerFactory)
        : base(readerFactory, writerFactory)
    {
    }

    protected override int MaxPlayers => ResistanceGame.MaxPlayers;

    protected override bool IsLocked(ResistanceGame game) => game.IsStarted;

    protected override IQueryable<ResistanceGame> Games(PartyKingDbContextBase dbContext)
    {
        // Split, so the three collections don't multiply into players x results x cards rows
        return dbContext.ResistanceGames
            .Include(x => x.Players)
            .Include(x => x.MissionResults)
            .Include(x => x.PlotCards)
            .AsSplitQuery();
    }

    protected override DbSet<ResistanceGame> Table(PartyKingDbContextBase dbContext) => dbContext.ResistanceGames;

    protected override DbSet<ResistancePlayer> PlayersOf(PartyKingDbContextBase dbContext) =>
        dbContext.ResistancePlayers;

    public async Task<ResistanceTeamResult> SelectTeamAsync(
        Guid sessionId, Guid? callerPlayerId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.NotStarted, game);
        }

        if (game.Phase == ResistancePhase.GameOver)
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.GameOver, game);
        }

        if (callerPlayerId is null || callerPlayerId != game.LeaderPlayerId)
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.NotLeader, game);
        }

        if (game.Phase is ResistancePhase.DealingCards or ResistancePhase.ResolvingCards)
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.CardsNotDealt, game);
        }

        if (game.Phase != ResistancePhase.SelectingTeam)
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.AlreadySubmitted, game);
        }

        if (playerIds.Count != game.Rules.TeamSize(game.Players.Count, game.MissionNumber)
            || playerIds.Distinct().Count() != playerIds.Count)
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.WrongSize, game);
        }

        if (playerIds.Any(id => game.Players.All(x => x.PlayerId != id)))
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.UnknownPlayer, game);
        }

        foreach (var player in game.Players)
        {
            player.IsOnTeam = playerIds.Contains(player.PlayerId);
            player.Vote = null;
            player.MissionCard = null;
        }

        game.Phase = ResistancePhase.Voting;
        await SaveAsync(dbContext, cancellationToken);

        return new ResistanceTeamResult(ResistanceTeamStatus.Selected, game);
    }

    public async Task<ResistanceDealResult> DealPlotCardsAsync(
        Guid sessionId, Guid? callerPlayerId, IReadOnlyList<PlotCardAssignment> assignments,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistanceDealResult(ResistanceDealStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return new ResistanceDealResult(ResistanceDealStatus.NotStarted, game);
        }

        if (game.Phase == ResistancePhase.GameOver)
        {
            return new ResistanceDealResult(ResistanceDealStatus.GameOver, game);
        }

        if (callerPlayerId is null || callerPlayerId != game.LeaderPlayerId)
        {
            return new ResistanceDealResult(ResistanceDealStatus.NotLeader, game);
        }

        if (game.Phase != ResistancePhase.DealingCards)
        {
            return new ResistanceDealResult(ResistanceDealStatus.NotDealing, game);
        }

        var toDeal = ResistanceRoles.CardsToDeal(game).ToDictionary(x => x.CardId);
        if (assignments.Any(x => !toDeal.ContainsKey(x.CardId))
            || assignments.Select(x => x.CardId).Distinct().Count() != assignments.Count)
        {
            return new ResistanceDealResult(ResistanceDealStatus.UnknownCard, game);
        }

        if (assignments.Any(x => x.PlayerId == game.LeaderPlayerId || game.Players.All(p => p.PlayerId != x.PlayerId)))
        {
            return new ResistanceDealResult(ResistanceDealStatus.InvalidReceiver, game);
        }

        if (assignments.Count != toDeal.Count)
        {
            return new ResistanceDealResult(ResistanceDealStatus.Incomplete, game);
        }

        foreach (var assignment in assignments)
        {
            var card = toDeal[assignment.CardId];
            // An Opinion Maker is played the moment it is dealt and stays face up
            card.Location = card.Type == ResistancePlotCardType.OpinionMaker
                ? ResistancePlotCardLocation.InPlay
                : ResistancePlotCardLocation.Hand;
            card.HolderPlayerId = assignment.PlayerId;
        }

        // The leader keeps the cards they play on themselves (Establish Confidence)
        foreach (var kept in game.PlotCards.Where(x => x.Location == ResistancePlotCardLocation.Drawn))
        {
            kept.Location = ResistancePlotCardLocation.Hand;
        }

        ResistanceRoles.AfterDeal(game);
        await SaveAsync(dbContext, cancellationToken);

        return new ResistanceDealResult(ResistanceDealStatus.Dealt, game);
    }

    public async Task<ResistancePlayCardResult> PlayPlotCardAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotStarted, game);
        }

        if (game.Phase == ResistancePhase.GameOver)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.GameOver, game);
        }

        var card = game.PlotCards.Find(x => x.CardId == cardId);
        var spotlight = card?.Type == ResistancePlotCardType.InTheSpotlight;
        if (spotlight && game.Phase != ResistancePhase.ConfidenceWindow)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotPlayingMission, game);
        }

        if (!spotlight && game.Phase != ResistancePhase.ResolvingCards)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotResolving, game);
        }

        if (card is null || !(spotlight || ResistanceRoles.IsImmediate(card.Type))
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.UnknownCard, game);
        }

        if (callerPlayerId is null || card.HolderPlayerId != callerPlayerId)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotAllowed, game);
        }

        if (card.Location == ResistancePlotCardLocation.Played)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongState, game);
        }

        if (spotlight && ResistanceRoles.OpenMissionPlayerId(game) is not null)
        {
            // One player is forced open per mission
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongState, game);
        }

        var validTarget = card.Type switch
        {
            ResistancePlotCardType.OverheardConversation =>
                ResistanceRoles.Neighbours(game, card.HolderPlayerId!.Value).Contains(targetPlayerId),
            ResistancePlotCardType.InTheSpotlight =>
                ResistanceRoles.CanBeForcedOpen(game, card.HolderPlayerId!.Value, targetPlayerId),
            _ => targetPlayerId != card.HolderPlayerId && game.Players.Any(x => x.PlayerId == targetPlayerId),
        };
        if (!validTarget)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.InvalidTarget, game);
        }

        card.Location = ResistancePlotCardLocation.Played;
        card.TargetPlayerId = targetPlayerId;
        card.PlayedAt = DateTimeOffset.UtcNow;
        card.PlayedInMission = game.MissionNumber;
        card.IsConfirmed = spotlight;
        await SaveAsync(dbContext, cancellationToken);

        return new ResistancePlayCardResult(ResistancePlayCardStatus.Done, game);
    }

    public async Task<ResistancePlayCardResult> TakeResponsibilityAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, bool? takeOpinionMaker,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotStarted, game);
        }

        if (game.Phase == ResistancePhase.GameOver)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.GameOver, game);
        }

        var card = game.PlotCards.Find(x => x.CardId == cardId);
        if (card is null || card.Type != ResistancePlotCardType.TakeResponsibility
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.UnknownCard, game);
        }

        if (callerPlayerId is null || card.HolderPlayerId != callerPlayerId)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotAllowed, game);
        }

        if (card.Location == ResistancePlotCardLocation.Played)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongState, game);
        }

        if (game.Phase is not (ResistancePhase.SelectingTeam or ResistancePhase.PlayingMission
            or ResistancePhase.PeekWindow or ResistancePhase.MissionResult))
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongPhase, game);
        }

        if (targetPlayerId == callerPlayerId || game.Players.All(x => x.PlayerId != targetPlayerId))
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.InvalidTarget, game);
        }

        var opinionMaker = game.PlotCards.Find(x => x.HolderPlayerId == targetPlayerId
                                                    && x.Location == ResistancePlotCardLocation.InPlay
                                                    && x.Type == ResistancePlotCardType.OpinionMaker);
        var hand = game.PlotCards
            .Where(x => x.HolderPlayerId == targetPlayerId && x.Location == ResistancePlotCardLocation.Hand)
            .ToList();
        if (opinionMaker is not null && hand.Count > 0 && takeOpinionMaker is null)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.ChoiceRequired, game);
        }

        var taken = opinionMaker is not null && (hand.Count == 0 || takeOpinionMaker == true)
            ? opinionMaker
            : hand.Count > 0 ? hand[Random.Shared.Next(hand.Count)] : null;
        if (taken is not null)
        {
            taken.HolderPlayerId = callerPlayerId;
        }

        card.Location = ResistancePlotCardLocation.Played;
        card.TargetPlayerId = targetPlayerId;
        card.PlayedAt = DateTimeOffset.UtcNow;
        card.PlayedInMission = game.MissionNumber;
        card.IsConfirmed = true;
        await SaveAsync(dbContext, cancellationToken);

        return new ResistancePlayCardResult(ResistancePlayCardStatus.Done, game);
    }

    public async Task<ResistancePlayCardResult> TakeLeadAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var (early, game) = await LoadStartedAsync(dbContext, sessionId, cancellationToken);
        if (early is { } status)
        {
            return new ResistancePlayCardResult(status, game);
        }

        var card = game!.PlotCards.Find(x => x.CardId == cardId);
        if (card is null || card.Type != ResistancePlotCardType.StrongLeader
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.UnknownCard, game);
        }

        if (callerPlayerId is null || card.HolderPlayerId != callerPlayerId)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotAllowed, game);
        }

        if (card.Location == ResistancePlotCardLocation.Played)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongState, game);
        }

        if (game.Phase != ResistancePhase.LeaderWindow)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongPhase, game);
        }

        if (callerPlayerId == game.LeaderPlayerId)
        {
            // Taking the lead from yourself changes nothing
            return new ResistancePlayCardResult(ResistancePlayCardStatus.InvalidTarget, game);
        }

        card.Location = ResistancePlotCardLocation.Played;
        card.TargetPlayerId = game.LeaderPlayerId;
        card.PlayedAt = DateTimeOffset.UtcNow;
        card.PlayedInMission = game.MissionNumber;
        card.IsConfirmed = true;
        game.LeaderPlayerId = callerPlayerId;
        ResistanceRoles.AfterWindow(game);
        await SaveAsync(dbContext, cancellationToken);

        return new ResistancePlayCardResult(ResistancePlayCardStatus.Done, game);
    }

    public async Task<ResistancePlayCardResult> PeekMissionCardAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var (early, game) = await LoadStartedAsync(dbContext, sessionId, cancellationToken);
        if (early is { } status)
        {
            return new ResistancePlayCardResult(status, game);
        }

        var card = game!.PlotCards.Find(x => x.CardId == cardId);
        if (card is null || card.Type != ResistancePlotCardType.KeepingCloseEye
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.UnknownCard, game);
        }

        if (callerPlayerId is null || card.HolderPlayerId != callerPlayerId)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotAllowed, game);
        }

        if (card.Location == ResistancePlotCardLocation.Played)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongState, game);
        }

        if (game.Phase != ResistancePhase.PeekWindow)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongPhase, game);
        }

        if (!ResistanceRoles.CanPeek(game, callerPlayerId.Value, targetPlayerId))
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.InvalidTarget, game);
        }

        card.Location = ResistancePlotCardLocation.Played;
        card.TargetPlayerId = targetPlayerId;
        card.PlayedAt = DateTimeOffset.UtcNow;
        card.PlayedInMission = game.MissionNumber;
        card.IsConfirmed = true;
        await SaveAsync(dbContext, cancellationToken);

        return new ResistancePlayCardResult(ResistancePlayCardStatus.Done, game);
    }

    public async Task<ResistancePlayCardResult> PlayNoConfidenceAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var (early, game) = await LoadStartedAsync(dbContext, sessionId, cancellationToken);
        if (early is { } status)
        {
            return new ResistancePlayCardResult(status, game);
        }

        var card = game!.PlotCards.Find(x => x.CardId == cardId);
        if (card is null || card.Type != ResistancePlotCardType.NoConfidence
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.UnknownCard, game);
        }

        if (callerPlayerId is null || card.HolderPlayerId != callerPlayerId)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotAllowed, game);
        }

        if (card.Location == ResistancePlotCardLocation.Played)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongState, game);
        }

        if (game.Phase != ResistancePhase.ConfidenceWindow)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongPhase, game);
        }

        card.Location = ResistancePlotCardLocation.Played;
        card.TargetPlayerId = game.LeaderPlayerId;
        card.PlayedAt = DateTimeOffset.UtcNow;
        card.PlayedInMission = game.MissionNumber;
        card.IsConfirmed = true;
        foreach (var player in game.Players)
        {
            player.PassedLeaderWindow = false;
        }

        // A rejected team never plays, so an In the Spotlight played in this window goes back to its holder's hand
        foreach (var spotlight in game.PlotCards.Where(x => x.Type == ResistancePlotCardType.InTheSpotlight
                                                           && x.Location == ResistancePlotCardLocation.Played
                                                           && x.PlayedInMission == game.MissionNumber))
        {
            spotlight.Location = ResistancePlotCardLocation.Hand;
            spotlight.TargetPlayerId = null;
            spotlight.PlayedAt = null;
            spotlight.PlayedInMission = 0;
            spotlight.IsConfirmed = false;
        }

        ResistanceRoles.RejectTeam(game);
        await SaveAsync(dbContext, cancellationToken);

        return new ResistancePlayCardResult(ResistancePlayCardStatus.Done, game);
    }

    public async Task<ResistancePlayCardResult> PassLeaderWindowAsync(
        Guid sessionId, Guid? callerPlayerId, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var (early, game) = await LoadStartedAsync(dbContext, sessionId, cancellationToken);
        if (early is { } status)
        {
            return new ResistancePlayCardResult(status, game);
        }

        var player = game!.Players.Find(x => x.PlayerId == callerPlayerId);
        if (player is null)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotPlayer, game);
        }

        if (!ResistanceRoles.IsConfirmationWindow(game))
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongPhase, game);
        }

        player.PassedLeaderWindow = true;
        if (game.Players.All(x => x.PassedLeaderWindow))
        {
            ResistanceRoles.AfterWindow(game);
        }

        await SaveAsync(dbContext, cancellationToken);

        return new ResistancePlayCardResult(ResistancePlayCardStatus.Done, game);
    }

    /// <summary>Loads the game and checks that it is started and not over; the status is null when it is.</summary>
    private async Task<(ResistancePlayCardStatus? Status, ResistanceGame? Game)> LoadStartedAsync(
        WriterDbContext dbContext, Guid sessionId, CancellationToken cancellationToken)
    {
        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return (ResistancePlayCardStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return (ResistancePlayCardStatus.NotStarted, game);
        }

        return game.Phase == ResistancePhase.GameOver
            ? (ResistancePlayCardStatus.GameOver, game)
            : (null, game);
    }

    public async Task<ResistancePlayCardResult> ConfirmPlotCardAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var (early, game) = await LoadResolvingAsync(dbContext, sessionId, cancellationToken);
        if (early is { } status)
        {
            return new ResistancePlayCardResult(status, game);
        }

        var card = game!.PlotCards.Find(x => x.CardId == cardId);
        if (card is null || !ResistanceRoles.IsImmediate(card.Type)
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.UnknownCard, game);
        }

        if (card.Location == ResistancePlotCardLocation.Hand || card.IsConfirmed)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongState, game);
        }

        if (callerPlayerId is null || ResistanceRoles.Viewer(card) != callerPlayerId)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotAllowed, game);
        }

        card.IsConfirmed = true;
        if (!ResistanceRoles.UnplayedImmediateCards(game).Any() && !ResistanceRoles.UnconfirmedCards(game).Any())
        {
            game.Phase = ResistancePhase.SelectingTeam;
        }

        await SaveAsync(dbContext, cancellationToken);

        return new ResistancePlayCardResult(ResistancePlayCardStatus.Done, game);
    }

    /// <summary>Loads the game and checks that the immediate cards are being played; the status is null when they are.</summary>
    private async Task<(ResistancePlayCardStatus? Status, ResistanceGame? Game)> LoadResolvingAsync(
        WriterDbContext dbContext, Guid sessionId, CancellationToken cancellationToken)
    {
        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return (ResistancePlayCardStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return (ResistancePlayCardStatus.NotStarted, game);
        }

        if (game.Phase == ResistancePhase.GameOver)
        {
            return (ResistancePlayCardStatus.GameOver, game);
        }

        return game.Phase == ResistancePhase.ResolvingCards
            ? (null, game)
            : (ResistancePlayCardStatus.NotResolving, game);
    }

    public async Task<ResistanceMissionCardResult> PlayMissionCardAsync(
        Guid sessionId, Guid? callerPlayerId, bool success, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistanceMissionCardResult(ResistanceMissionStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return new ResistanceMissionCardResult(ResistanceMissionStatus.NotStarted, game);
        }

        if (game.Phase == ResistancePhase.GameOver)
        {
            return new ResistanceMissionCardResult(ResistanceMissionStatus.GameOver, game);
        }

        var player = game.Players.FirstOrDefault(x => x.PlayerId == callerPlayerId);
        if (player is null)
        {
            return new ResistanceMissionCardResult(ResistanceMissionStatus.NotPlayer, game);
        }

        if (game.Phase != ResistancePhase.PlayingMission)
        {
            return new ResistanceMissionCardResult(ResistanceMissionStatus.NotPlaying, game);
        }

        if (!player.IsOnTeam)
        {
            return new ResistanceMissionCardResult(ResistanceMissionStatus.NotOnTeam, game);
        }

        if (player.MissionCard is not null)
        {
            return new ResistanceMissionCardResult(ResistanceMissionStatus.AlreadyPlayed, game);
        }

        if (!success && player.Role == ResistanceRole.Resistance)
        {
            return new ResistanceMissionCardResult(ResistanceMissionStatus.MustSucceed, game);
        }

        player.MissionCard = success;
        if (ResistanceRoles.AllCardsPlayed(game))
        {
            ResistanceRoles.ResolveMission(game);
        }

        await SaveAsync(dbContext, cancellationToken);

        return new ResistanceMissionCardResult(ResistanceMissionStatus.Played, game);
    }

    public async Task<ResistanceContinueResult> ContinueAsync(
        Guid sessionId, Guid? callerPlayerId, bool isManager, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistanceContinueResult(ResistanceContinueStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return new ResistanceContinueResult(ResistanceContinueStatus.NotStarted, game);
        }

        if (game.Phase == ResistancePhase.GameOver)
        {
            return new ResistanceContinueResult(ResistanceContinueStatus.GameOver, game);
        }

        if (!isManager && (callerPlayerId is null || callerPlayerId != game.LeaderPlayerId))
        {
            return new ResistanceContinueResult(ResistanceContinueStatus.NotAllowed, game);
        }

        if (game.Phase != ResistancePhase.MissionResult)
        {
            return new ResistanceContinueResult(ResistanceContinueStatus.NothingToContinue, game);
        }

        ResistanceRoles.PassLeadership(game);
        await SaveAsync(dbContext, cancellationToken);

        return new ResistanceContinueResult(ResistanceContinueStatus.Continued, game);
    }
}
