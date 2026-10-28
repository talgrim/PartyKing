using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authorization;
using PartyKing.Application.Queue.Services;
using PartyKing.Contract.V1.Sessions;
using PartyKing.Contract.V1.Spotify;
using PartyKing.Infrastructure.Authentication;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers;

public class FallbackPlaylistController : CoreController
{
    private readonly IFallbackPlaylistService _fallbackPlaylistService;
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;

    public FallbackPlaylistController(
        IHttpContextAccessor httpContextAccessor,
        IFallbackPlaylistService fallbackPlaylistService,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor)
    {
        _fallbackPlaylistService = fallbackPlaylistService;
        _cookieAuthorizationHandler = cookieAuthorizationHandler;
    }

    /// <summary>
    /// Host or admin: sets the playlist played when the queue is empty, or clears it. 400 for a playlist the host
    /// does not own, 409 when the session has no host.
    /// </summary>
    [HttpPut("sessions/{sessionId:guid}/fallback-playlist")]
    [RequireSessionAccess]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetFallbackPlaylist(
        Guid sessionId,
        [FromBody] SetFallbackPlaylistRequest request,
        CancellationToken cancellationToken)
    {
        var profileId = _cookieAuthorizationHandler.TryReadProfileId(Request, out var id) ? id : null;
        var isAdmin = (await HttpContext.AuthenticateAsync(Constants.AdminScheme)).Succeeded;

        var result = await _fallbackPlaylistService.SetAsync(
            sessionId,
            request.Playlist,
            request.Shuffle,
            profileId,
            isAdmin,
            cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }

    /// <summary>The playlists the logged-in host owns, to choose the fallback playlist from.</summary>
    [HttpGet("sessions/{sessionId:guid}/host-playlists")]
    [RequireSessionAccess]
    [AuthorizeByCookie]
    [ProducesResponseType<IReadOnlyList<PlaylistDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHostPlaylists(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _fallbackPlaylistService.GetHostPlaylistsAsync(sessionId, GetProfileId(), cancellationToken);

        return result.Match(Ok, Problem);
    }
}
