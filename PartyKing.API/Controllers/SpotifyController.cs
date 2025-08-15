using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authentication;
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

    [HttpGet("auth-url")]
    [ProducesResponseType<Uri>(StatusCodes.Status200OK)]
    public IActionResult GetAuthenticationUrl()
    {
        return Ok(_spotifyService.GetAuthenticationUrl());
    }

    [HttpPost("authenticate")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Authenticate([FromBody] string code, CancellationToken cancellationToken)
    {
        var result = await _spotifyService.AuthenticateByCodeAsync(code, cancellationToken);

        return result.Match(
            success => Ok(success.Value),
            Problem);
    }

    [HttpGet]
    [Authorize(Policies.AuthenticatedUser)]
    [Route("currently-playing")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentlyPlaying(CancellationToken cancellationToken)
    {
        var result = await _spotifyService.GetCurrentlyPlayingAsync(GetSpotifyAccessToken(), cancellationToken);

        return Ok(result);
    }
}