using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PartyKing.API.Configuration;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Domain.Enums;

namespace PartyKing.API.Controllers;

public class SlideshowController : CoreController
{
    private readonly ISlideshowService _slideshowService;
    private readonly ILogger<SlideshowController> _logger;

    public SlideshowController(
        IHttpContextAccessor httpContextAccessor,
        ISlideshowService slideshowService,
        IOptions<SlideshowSettings> slideshowSettingsOptions,
        IWebHostEnvironment webHostEnvironment,
        ILogger<SlideshowController> logger)
        : base(httpContextAccessor, slideshowSettingsOptions, webHostEnvironment)
    {
        _slideshowService = slideshowService;
        _logger = logger;

        if (!_slideshowService.IsInitialized())
        {
            var placeholderPath = Path.Combine(GetPhysicalRoot(), SlideshowSettings.PlaceholderPhotosDirectory);
            _slideshowService.UpdateSettings(false, placeholderPath);
        }
    }

    [HttpPut("UploadPhoto")]
    public async Task<IActionResult> UploadPhoto([Required] IFormFile[] files, CancellationToken cancellationToken)
    {
        try
        {
            var root = Path.Combine(GetPhysicalRoot(), SlideshowSettings.UploadedPhotosDirectory);
            await _slideshowService.UploadImagesAsync(files, root, cancellationToken);
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

        if (result is null)
        {
            _logger.LogWarning("No image found to display");
            return NoContent();
        }

        var rootPath = GetPhysicalRoot();

        switch (result.ImageSource)
        {
            case SlideshowImageSource.Placeholder:
                rootPath = Path.Combine(rootPath, SlideshowSettings.PlaceholderPhotosDirectory);
                break;

            case SlideshowImageSource.Uploaded:
                rootPath = Path.Combine(rootPath, SlideshowSettings.UploadedPhotosDirectory);
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        var contentPath = Path.Combine(rootPath, result.ImageUrl);
        var contentType = result.ContentType;

        _logger.LogInformation("Returning photo {FilePath} ({ContentType})", contentPath, contentType);

        return Ok(contentPath);
    }
}