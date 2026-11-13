using ErrorOr;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Demo.Helpers;
using PartyKing.Application.Demo.Models;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Demo;
using PartyKing.Domain.Entities.Demo;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Repositories;

namespace PartyKing.Application.Demo.Services;

public interface IDemoSessionService
{
    /// <summary>
    /// The sessions among <paramref name="started"/> that were really started by that host profile and have not ended,
    /// the one that ends last first. Not found when the server is not a demo.
    /// </summary>
    Task<ErrorOr<MyDemoSessionsDto>> ListStartedAsync(
        IReadOnlyCollection<StartedDemoSession> started,
        CancellationToken cancellationToken);

    /// <summary>
    /// What it takes to host a session again that the caller started and that has not ended (the same as at its
    /// start, with the time that is left). Not found for a session that is not theirs, is over or is no demo one.
    /// </summary>
    Task<ErrorOr<DemoStart>> ResumeAsync(StartedDemoSession started, CancellationToken cancellationToken);

    /// <summary>When the demo session ends; not found for a session that is not a demo one.</summary>
    Task<ErrorOr<DemoSessionDto>> GetAsync(Guid sessionId, CancellationToken cancellationToken);
}

internal class DemoSessionService : IDemoSessionService
{
    private readonly DemoConfiguration _configuration;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly IDemoSessionsRepository _demoSessionsRepository;
    private readonly IDateTimeProvider _clock;

    public DemoSessionService(
        IOptions<DemoConfiguration> options,
        ISessionsRepository sessionsRepository,
        IDemoSessionsRepository demoSessionsRepository,
        IDateTimeProvider clock)
    {
        _configuration = options.Value;
        _sessionsRepository = sessionsRepository;
        _demoSessionsRepository = demoSessionsRepository;
        _clock = clock;
    }

    public async Task<ErrorOr<MyDemoSessionsDto>> ListStartedAsync(
        IReadOnlyCollection<StartedDemoSession> started,
        CancellationToken cancellationToken)
    {
        if (!_configuration.Enabled)
        {
            return DemoErrors.NotEnabled();
        }

        var sessions = new List<MyDemoSessionDto>();
        foreach (var entry in started.DistinctBy(x => x.SessionId))
        {
            if (await FindLiveAsync(entry, cancellationToken) is { } live)
            {
                sessions.Add(new MyDemoSessionDto(entry.SessionId, live.Session.Name, live.Demo.ExpiresAtUtc()));
            }
        }

        return new MyDemoSessionsDto([.. sessions.OrderByDescending(x => x.ExpiresAt)], _clock.UtcNow);
    }

    public async Task<ErrorOr<DemoStart>> ResumeAsync(
        StartedDemoSession started,
        CancellationToken cancellationToken)
    {
        if (!_configuration.Enabled)
        {
            return DemoErrors.NotEnabled();
        }

        if (await FindLiveAsync(started, cancellationToken) is not { } live)
        {
            return DemoErrors.SessionGone();
        }

        return new DemoStart(
            live.Session.Id,
            started.ProfileId,
            live.Session.AccessStamp,
            live.Demo.ExpiresAtUtc());
    }

    public async Task<ErrorOr<DemoSessionDto>> GetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        // A server that is not a demo has no rows, so it answers like any session that is not a demo one
        var demoSession = await _demoSessionsRepository.GetAsync(sessionId, cancellationToken);

        return demoSession is null
            ? DemoErrors.NotADemoSession()
            : new DemoSessionDto(demoSession.ExpiresAtUtc(), _clock.UtcNow);
    }

    /// <summary>
    /// The session and its demo row when the host profile is the one that started it and it has not ended; the sweep
    /// deletes a session up to a minute late, so the clock decides, not the row.
    /// </summary>
    private async Task<(Session Session, DemoSession Demo)?> FindLiveAsync(
        StartedDemoSession started,
        CancellationToken cancellationToken)
    {
        var demo = await _demoSessionsRepository.GetAsync(started.SessionId, cancellationToken);
        if (demo is null || demo.HostProfileId != started.ProfileId || demo.IsExpired(_clock.UtcNow.UtcDateTime))
        {
            return null;
        }

        var session = await _sessionsRepository.GetAsync(started.SessionId, cancellationToken);

        return session is null ? null : (session, demo);
    }
}
