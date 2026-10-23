using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authorization;
using PartyKing.Application.Spotify.Services;
using PartyKing.Contract.V1.Spotify;

namespace PartyKing.API.Controllers;

public class SpotifyController : CoreController
{
    private readonly ISpotifyService _spotifyService;

    public SpotifyController(
        IHttpContextAccessor httpContextAccessor,
        ISpotifyService spotifyService)
        : base(httpContextAccessor)
    {
        _spotifyService = spotifyService;
    }

    /// <summary>Starts the Spotify login that makes the caller the host of the session.</summary>
    [HttpPost("sessions/{sessionId:guid}/host-login-urls")]
    [RequireSessionAccess]
    [ProducesResponseType<Uri>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateHostLoginUrl(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _spotifyService.GetHostLoginUrlAsync(sessionId, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Completes the Spotify login and claims the session. 409 if someone else already owns it.</summary>
    [HttpPost("authenticate")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Authenticate(
        [FromBody] AuthenticateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _spotifyService.AuthenticateByCodeAsync(request.Code, request.State, cancellationToken);

        return result.Match(Ok, Problem);
    }

    [HttpGet("sessions/{sessionId:guid}/tracks")]
    [RequireSessionAccess]
    [ProducesResponseType<IReadOnlyList<SpotifyTrackDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchTracks(
        Guid sessionId,
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        var result = await _spotifyService.SearchTracksAsync(sessionId, query, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Access token of the logged-in host, used by the browser's Spotify Web Playback SDK.</summary>
    [HttpGet("spotify/token")]
    [AuthorizeByCookie]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public IActionResult GetAccessToken()
    {
        return Ok(GetSpotifyAccessToken());
    }
}
