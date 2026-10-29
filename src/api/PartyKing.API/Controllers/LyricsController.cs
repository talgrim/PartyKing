using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authorization;
using PartyKing.Application.Lyrics.Services;
using PartyKing.Contract.V1.Lyrics;

namespace PartyKing.API.Controllers;

public class LyricsController : CoreController
{
    private readonly ILyricsService _lyricsService;

    public LyricsController(
        IHttpContextAccessor httpContextAccessor,
        ILyricsService lyricsService)
        : base(httpContextAccessor)
    {
        _lyricsService = lyricsService;
    }

    /// <summary>
    /// The synchronized lyrics of the song being played, for anyone who unlocked the session. 404 when nothing
    /// plays or no lyrics were found.
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/now-playing/lyrics")]
    [RequireSessionAccess]
    [ProducesResponseType<LyricsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLyrics(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _lyricsService.GetForPlayingSongAsync(sessionId, cancellationToken);

        return result.Match(Ok, Problem);
    }
}
