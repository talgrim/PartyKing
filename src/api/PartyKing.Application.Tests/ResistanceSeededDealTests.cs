using PartyKing.Contract.V1.Games.Resistance;

namespace PartyKing.Application.Tests;

public class ResistanceSeededDealTests
{
    private sealed class Table(int seed) : ResistanceServiceTestBase(new FakeRandomProvider(seed))
    {
        /// <summary>The first leader and the spies by name, as <c>leader:spy,spy</c>.</summary>
        public async Task<string> DealAsync(int count)
        {
            var ct = CancellationToken.None;
            var callers = await OpenGameAndJoinAsync(count);
            var started = (await _roundService.StartAsync(_id, Host, ct)).Value;
            var spies = new List<string>();
            foreach (var caller in callers)
            {
                var view = (await _lobbyService.GetAsync(_id, caller, ct)).Value;
                if (view.YourRole == ResistanceRoleDto.Spy)
                {
                    spies.Add(started.Players.First(x => x.Id == caller.PlayerId).Name);
                }
            }

            var leader = started.Players.First(x => x.Id == started.LeaderPlayerId).Name;

            return $"{leader}:{string.Join(',', spies.Order())}";
        }
    }

    [Fact]
    public async Task Start_WithTheSameSeed_DealsTheSameRolesAndLeader()
    {
        var first = await new Table(11).DealAsync(8);
        var second = await new Table(11).DealAsync(8);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Start_WithDifferentSeeds_CanDealDifferently()
    {
        var deals = new HashSet<string>();
        for (var seed = 1; seed <= 8; seed++)
        {
            deals.Add(await new Table(seed).DealAsync(8));
        }

        Assert.True(deals.Count > 1);
    }
}
