using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using PartyKing.Application.Slideshow.Services;

namespace PartyKing.API.Controllers;

public class SlideshowController : CoreController
{
    private readonly ISlideshowService _slideshowService;

    public SlideshowController(
        IHttpContextAccessor httpContextAccessor,
        ISlideshowService slideshowService) : base(httpContextAccessor)
    {
        _slideshowService = slideshowService;
    }

    [HttpPut("UploadPhoto")]
    public async Task<IActionResult> UploadPhoto([Required] IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            await _slideshowService.UploadImageAsync(file, cancellationToken);
            return Ok();
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

    [HttpGet("GetPhoto")]
    public async Task<IActionResult> GetPhoto(CancellationToken cancellationToken)
    {
        var result = await _slideshowService.GetImageAsync(cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }
}