using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authorization;
using PartyKing.Application.Playlists.Services;
using PartyKing.Contract.V1.Playlists;

namespace PartyKing.API.Controllers;

public class SavedPlaylistsController : CoreController
{
    private readonly ISavedPlaylistService _savedPlaylistService;

    public SavedPlaylistsController(
        IHttpContextAccessor httpContextAccessor,
        ISavedPlaylistService savedPlaylistService)
        : base(httpContextAccessor)
    {
        _savedPlaylistService = savedPlaylistService;
    }

    /// <summary>
    /// Saves the chosen parts of the session's queue as a new playlist of the signed-in account. 400 when the name is
    /// blank or too long, nothing is chosen, or the account is at its limit; 401 without an account.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/saved-playlists")]
    [RequireSessionAccess]
    [AuthorizeAccount]
    [ProducesResponseType<SavedPlaylistDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveQueue(
        Guid sessionId,
        [FromBody] SaveQueueAsPlaylistRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _savedPlaylistService.SaveQueueAsync(
            GetAccountId(),
            sessionId,
            request.Name,
            request.Sections ?? [],
            cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>How many songs each part of the session's queue holds now, for the dialog that saves it.</summary>
    [HttpGet("sessions/{sessionId:guid}/saved-playlists/preview")]
    [RequireSessionAccess]
    [AuthorizeAccount]
    [ProducesResponseType<SavedPlaylistPreviewDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPreview(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _savedPlaylistService.GetPreviewAsync(sessionId, cancellationToken);

        return result.Match(Ok, Problem);
    }
}
