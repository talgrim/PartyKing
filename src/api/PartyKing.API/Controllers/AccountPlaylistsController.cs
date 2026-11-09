using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authorization;
using PartyKing.Application.Playlists.Services;
using PartyKing.Contract.V1.Playlists;

namespace PartyKing.API.Controllers;

public class AccountPlaylistsController : CoreController
{
    private readonly ISavedPlaylistService _savedPlaylistService;

    public AccountPlaylistsController(
        IHttpContextAccessor httpContextAccessor,
        ISavedPlaylistService savedPlaylistService)
        : base(httpContextAccessor)
    {
        _savedPlaylistService = savedPlaylistService;
    }

    /// <summary>The playlists the signed-in account saved, the newest first; 401 without an account.</summary>
    [HttpGet("account/saved-playlists")]
    [AuthorizeAccount]
    [ProducesResponseType<IReadOnlyList<SavedPlaylistDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSavedPlaylists(CancellationToken cancellationToken)
    {
        var result = await _savedPlaylistService.ListAsync(GetAccountId(), cancellationToken);

        return result.Match(Ok, Problem);
    }
}
