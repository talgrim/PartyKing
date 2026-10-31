using PartyKing.Application.Admin.Services;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Application.Games.Services;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Games.Resistance;

namespace PartyKing.Application.Tests;

public abstract class ResistanceServiceTestBase
{
    private protected const string HostId = "host-1";

    private protected static readonly GameCaller Admin = new(null, true);
    private protected static readonly GameCaller Host = new(HostId, false);

    private protected readonly FakeSessionsRepository _sessions = new();
    private protected readonly FakeResistanceGamesRepository _games = new();
    private protected readonly FakeSessionMusicNotifier _notifier = new();
    private protected readonly FakeRulesProvider _rules = new();
    private protected readonly ResistanceLobbyService _lobbyService;
    private protected readonly ResistanceRoundService _roundService;
    private protected readonly ResistancePlotCardService _plotCardService;
    private protected readonly Guid _id;

    private protected ResistanceServiceTestBase()
    {
        var clock = new FakeClock();
        var sessionAuthorizer = new SessionAuthorizer(_sessions);
        var lobby = new ResistanceGameLobby(
            sessionAuthorizer, _sessions, _games, clock, new ResistanceLobbyNotifier(_notifier));
        var gate = new ResistanceActionGate(lobby, _notifier);
        _lobbyService = new ResistanceLobbyService(lobby, gate, _games, clock, _notifier, _rules);
        _roundService = new ResistanceRoundService(gate, _games, clock);
        _plotCardService = new ResistancePlotCardService(gate, _games);
        _id = _sessions.AddSession(HostId, new PasswordHasher().Hash("secret")).Id;
    }

    private protected async Task<(List<GameCaller> Callers, GameCaller Leader)> StartWithTeamAsync(int count)
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(count);
        var started = (await _roundService.StartAsync(_id, Host, ct)).Value;
        var leader = callers.First(x => x.PlayerId == started.LeaderPlayerId);
        await _roundService.SelectTeamAsync(_id, started.Players.Take(2).Select(x => x.Id).ToList(), leader, ct);

        return (callers, leader);
    }

    /// <summary>
    /// Starts a game whose leader submits a team of <paramref name="spiesOnTeam"/> spies filled up with members of the
    /// resistance, and everybody approves it. Spies and the resistance are told apart by the role each one sees.
    /// </summary>
    private protected async Task<(List<GameCaller> Team, List<GameCaller> Spies, List<GameCaller> Resistance)> StartMissionAsync(
        int count, int spiesOnTeam)
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(count);
        var started = (await _roundService.StartAsync(_id, Host, ct)).Value;

        var spies = new List<GameCaller>();
        var resistance = new List<GameCaller>();
        foreach (var caller in callers)
        {
            var role = (await _lobbyService.GetAsync(_id, caller, ct)).Value.YourRole;
            (role == ResistanceRoleDto.Spy ? spies : resistance).Add(caller);
        }

        var team = spies.Take(spiesOnTeam).Concat(resistance).Take(started.RequiredTeamSize!.Value).ToList();
        var leader = callers.First(x => x.PlayerId == started.LeaderPlayerId);
        var selected = await _roundService.SelectTeamAsync(_id, team.Select(x => x.PlayerId!.Value).ToList(), leader, ct);
        Assert.False(selected.IsError);
        await VoteAsync(callers, Enumerable.Repeat(true, count).ToArray());

        return (
            team,
            spies.Where(team.Contains).ToList(),
            resistance.Where(team.Contains).ToList());
    }

    /// <summary>
    /// Plays one whole mission: the leader picks a team (with a spy to play Fail when <paramref name="fail"/>, else
    /// only members of the resistance), everybody approves and the team plays.
    /// </summary>
    private protected async Task PlayRoundAsync(List<GameCaller> callers, bool fail)
    {
        var ct = CancellationToken.None;
        var game = (await _lobbyService.GetAsync(_id, Host, ct)).Value;
        var roles = new Dictionary<Guid, ResistanceRoleDto>();
        foreach (var caller in callers)
        {
            roles[caller.PlayerId!.Value] = (await _lobbyService.GetAsync(_id, caller, ct)).Value.YourRole!.Value;
        }

        var spies = callers.Where(x => roles[x.PlayerId!.Value] == ResistanceRoleDto.Spy);
        var resistance = callers.Where(x => roles[x.PlayerId!.Value] == ResistanceRoleDto.Resistance);
        var team = (fail ? spies.Take(1).Concat(resistance) : resistance).Take(game.RequiredTeamSize!.Value).ToList();

        var leader = callers.First(x => x.PlayerId == game.LeaderPlayerId);
        Assert.False((await _roundService.SelectTeamAsync(_id, team.Select(x => x.PlayerId!.Value).ToList(), leader, ct)).IsError);
        await VoteAsync(callers, Enumerable.Repeat(true, callers.Count).ToArray());
        foreach (var caller in team)
        {
            var success = roles[caller.PlayerId!.Value] == ResistanceRoleDto.Resistance;
            Assert.False((await _roundService.PlayMissionCardAsync(_id, success, caller, ct)).IsError);
        }
    }

    /// <summary>Every player votes, in joining order.</summary>
    private protected async Task VoteAsync(List<GameCaller> callers, params bool[] approvals)
    {
        for (var i = 0; i < callers.Count; i++)
        {
            var result = await _roundService.VoteAsync(_id, approvals[i], callers[i], CancellationToken.None);
            Assert.False(result.IsError);
        }
    }

    private protected async Task<List<GameCaller>> JoinPlayersAsync(int count)
    {
        var callers = new List<GameCaller>();
        for (var i = 1; i <= count; i++)
        {
            callers.Add(await JoinAsync($"Player {i}"));
        }

        return callers;
    }

    private protected async Task<List<GameCaller>> OpenGameAndJoinAsync(int count)
    {
        await OpenGameAsync();

        return await JoinPlayersAsync(count);
    }

    private protected async Task OpenGameAsync()
    {
        await _lobbyService.CreateAsync(_id, Host, CancellationToken.None);
        await _lobbyService.OpenAsync(_id, Admin, CancellationToken.None);
    }

    private protected async Task<GameCaller> JoinAsync(string name)
    {
        var joined = await _lobbyService.JoinAsync(_id, name, new GameCaller("guest", false), CancellationToken.None);

        return new GameCaller("guest", false, joined.Value);
    }
}
