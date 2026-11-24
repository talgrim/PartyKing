using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PartyKing.API.Workers;
using PartyKing.Application.Configuration;
using PartyKing.Application.Sessions.Services;

namespace PartyKing.Api.Tests;

public class SessionDeletionWorkerTests
{
    private static (SessionDeletionWorker Worker, FakeSweep Sweep) Create(int intervalSeconds = 3600, FakeSweep? sweep = null)
    {
        sweep ??= new FakeSweep();
        var services = new ServiceCollection().AddScoped<IScheduledSessionDeletionSweep>(_ => sweep).BuildServiceProvider();
        var worker = new SessionDeletionWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SessionDeletionConfiguration { SweepIntervalSeconds = intervalSeconds }),
            NullLogger<SessionDeletionWorker>.Instance);

        return (worker, sweep);
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }

        return condition();
    }

    [Fact]
    public async Task TheWorker_SweepsOnceAtStartup_WithoutWaitingForTheFirstTick()
    {
        var (worker, sweep) = Create(intervalSeconds: 3600);

        await worker.StartAsync(CancellationToken.None);
        var swept = await WaitForAsync(() => sweep.Sweeps >= 1);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(swept);
        Assert.Equal(1, sweep.Sweeps);
    }

    [Fact]
    public async Task TheWorker_SweepsAgainOnEveryTick()
    {
        var (worker, sweep) = Create(intervalSeconds: 1);

        await worker.StartAsync(CancellationToken.None);
        var sweptTwice = await WaitForAsync(() => sweep.Sweeps >= 2);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(sweptTwice);
    }

    [Fact]
    public async Task ASweepThatThrows_DoesNotStopTheWorker()
    {
        var failing = new FakeSweep { ThrowOnFirst = true };
        var (worker, sweep) = Create(intervalSeconds: 1, failing);

        await worker.StartAsync(CancellationToken.None);
        var recovered = await WaitForAsync(() => sweep.Sweeps >= 2);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(recovered);
    }

    [Fact]
    public void TheSweepInterval_IsAtLeastASecond_BecauseATimerRefusesZero()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), new SessionDeletionConfiguration { SweepIntervalSeconds = 0 }.SweepInterval);
        Assert.Equal(TimeSpan.FromSeconds(1), new SessionDeletionConfiguration { SweepIntervalSeconds = -5 }.SweepInterval);
        Assert.Equal(TimeSpan.FromHours(1), new SessionDeletionConfiguration().SweepInterval);
    }

    [Fact]
    public void TheDelay_DefaultsToSevenDays_AndAllowsAFraction()
    {
        Assert.Equal(TimeSpan.FromDays(7), new SessionDeletionConfiguration().Delay);
        Assert.Equal(TimeSpan.FromSeconds(5), new SessionDeletionConfiguration { DelayDays = 5.0 / 86400 }.Delay);
    }

    private sealed class FakeSweep : IScheduledSessionDeletionSweep
    {
        private int _sweeps;

        public int Sweeps => Volatile.Read(ref _sweeps);

        public bool ThrowOnFirst { get; set; }

        public Task<int> SweepAsync(CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref _sweeps);

            if (ThrowOnFirst && count == 1)
            {
                throw new InvalidOperationException("the database is locked");
            }

            return Task.FromResult(0);
        }
    }
}
