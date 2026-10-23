using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PartyKing.API.Authorization;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Contract.V1.Slideshow;

namespace PartyKing.API.Controllers;

public class SlideshowController : CoreController
{
    public const string UploadRateLimitPolicy = "photo-upload";

    // Limits for one upload request (a guest can pick several photos); the per-file limit is checked by the service
    private const long MaxUploadRequestBytes = 100L * 1024 * 1024;

    private readonly ISlideshowService _slideshowService;
    private readonly ILogger<SlideshowController> _logger;

    public SlideshowController(
        IHttpContextAccessor httpContextAccessor,
        ISlideshowService slideshowService,
        ILogger<SlideshowController> logger)
        : base(httpContextAccessor)
    {
        _slideshowService = slideshowService;
        _logger = logger;
    }

    [HttpPut("sessions/{sessionId:guid}/photos/{deleteAfterPresentation:bool}")]
    [RequireSessionAccess]
    [EnableRateLimiting(UploadRateLimitPolicy)]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadRequestBytes)]
    public async Task<IActionResult> UploadPhoto(
        Guid sessionId,
        [Required] IFormFile[] files,
        bool deleteAfterPresentation,
        CancellationToken cancellationToken)
    {
        try
        {
            await _slideshowService.UploadImagesAsync(sessionId, files, deleteAfterPresentation, cancellationToken);
            return Ok();
        }
        catch (InvalidUploadException e)
        {
            return BadRequest(e.Message);
        }
    }

    /// <summary>The photo the session's slideshow shows now; 204 when nothing was uploaded.</summary>
    [HttpGet("sessions/{sessionId:guid}/photos/current")]
    [RequireSessionAccess]
    [ProducesResponseType<SlideshowImageDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentPhoto(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _slideshowService.GetImageAsync(sessionId, cancellationToken);

        if (result is null)
        {
            _logger.LogDebug("Session {SessionId} has no image to display", sessionId);
            return NoContent();
        }

        return Ok(result);
    }

    [HttpGet("admin/sessions/{sessionId:guid}/slideshow-settings")]
    [AuthorizeAdmin]
    [ProducesResponseType<SlideshowSettingsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _slideshowService.GetSettingsAsync(sessionId, cancellationToken);

        return result.Match(Ok, Problem);
    }

    [HttpPut("admin/sessions/{sessionId:guid}/slideshow-settings")]
    [AuthorizeAdmin]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateSettings(
        Guid sessionId,
        [FromBody] SlideshowSettingsDto settings,
        CancellationToken cancellationToken)
    {
        var result = await _slideshowService.UpdateSettingsAsync(sessionId, settings, cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }
}
