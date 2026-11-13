using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PartyKing.API.Authorization;
using PartyKing.API.Cookies;
using PartyKing.Application.Demo.Services;
using PartyKing.Contract.V1.Demo;

namespace PartyKing.API.Controllers;

public class DemoController : CoreController
{
    public const string StartRateLimitPolicy = "demo-start";

    private readonly IDemoSessionService _demoSessionService;
    private readonly ISignedCookies _cookies;

    public DemoController(
        IHttpContextAccessor httpContextAccessor,
        IDemoSessionService demoSessionService,
        ISignedCookies cookies)
        : base(httpContextAccessor)
    {
        _demoSessionService = demoSessionService;
        _cookies = cookies;
    }

    /// <summary>Whether this server is a demo. Public: the landing page asks it before it shows anything.</summary>
    [HttpGet("demo/config")]
    [ProducesResponseType<DemoConfigDto>(StatusCodes.Status200OK)]
    public IActionResult GetConfig()
    {
        return Ok(_demoSessionService.GetConfig());
    }

    /// <summary>
    /// Starts a session for the visitor and makes them its host: the cookies are the ones a Spotify host login and the
    /// session password would have set. 404 on a server that is not a demo, 409 while the demo is full.
    /// </summary>
    [HttpPost("demo/sessions")]
    [EnableRateLimiting(StartRateLimitPolicy)]
    [ProducesResponseType<DemoStartedDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Start(CancellationToken cancellationToken)
    {
        var result = await _demoSessionService.StartAsync(cancellationToken);

        if (result.IsError)
        {
            return Problem(result.Errors);
        }

        var start = result.Value;
        _cookies.GrantHost(start.ProfileId, HttpContext);
        _cookies.GrantSpotifyAccessToken(start.AccessToken, start.AccessTokenExpiresIn, HttpContext);
        _cookies.GrantSessionAccess(start.SessionId, start.AccessStamp, HttpContext);

        return Ok(new DemoStartedDto(start.SessionId, start.ExpiresAt));
    }

    /// <summary>When the session ends. 404 for a session that is not a demo one.</summary>
    [HttpGet("sessions/{sessionId:guid}/demo")]
    [RequireSessionAccess]
    [ProducesResponseType<DemoSessionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _demoSessionService.GetAsync(sessionId, cancellationToken);

        return result.Match(Ok, Problem);
    }
}
