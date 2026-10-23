using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PartyKing.API.Authorization;
using PartyKing.Application.Configuration;
using PartyKing.Application.Mappings;
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
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly SlideshowConfiguration _slideshowSettings;

    public SlideshowController(
        IHttpContextAccessor httpContextAccessor,
        ISlideshowService slideshowService,
        IOptions<SlideshowConfiguration> slideshowSettingsOptions,
        IWebHostEnvironment webHostEnvironment,
        ILogger<SlideshowController> logger)
        : base(httpContextAccessor)
    {
        _slideshowService = slideshowService;
        _slideshowSettings = slideshowSettingsOptions.Value;
        _webHostEnvironment = webHostEnvironment;
        _logger = logger;
    }

    [HttpPut("upload-photo/{deleteAfterPresentation:bool}")]
    [EnableRateLimiting(UploadRateLimitPolicy)]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadRequestBytes)]
    public async Task<IActionResult> UploadPhoto([Required] IFormFile[] files, bool deleteAfterPresentation, CancellationToken cancellationToken)
    {
        try
        {
            await _slideshowService.UploadImagesAsync(files, deleteAfterPresentation, cancellationToken);
            return Ok();
        }
        catch (InvalidUploadException e)
        {
            return BadRequest(e.Message);
        }
    }

    [HttpGet("get-photo")]
    [ProducesResponseType<SlideshowImageDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPhoto(CancellationToken cancellationToken)
    {
        var result = await _slideshowService.GetImageAsync(cancellationToken);

        if (result is null)
        {
            _logger.LogWarning("No image found to display");
            return NoContent();
        }

        return Ok(result);
    }

    [HttpGet("get-all")]
    [AuthorizeAdmin]
    [ProducesResponseType<IEnumerable<ImageDataDto>>(StatusCodes.Status200OK)]
    public IActionResult GetAll()
    {
        return Ok(GetUploadedImages());
    }

    [HttpGet("get-configuration")]
    [AuthorizeAdmin]
    [ProducesResponseType<SlideshowConfiguration>(StatusCodes.Status200OK)]
    public IActionResult GetConfiguration()
    {
        return Ok(_slideshowSettings);
    }

    [HttpPost("update-configuration")]
    [AuthorizeAdmin]
    public async Task<IActionResult> UpdateConfiguration(
        [FromBody] SlideshowSettingsDto settings,
        CancellationToken cancellationToken)
    {
        var mappedSettings = settings.ToDomain(_slideshowSettings.SlideTime);
        await _slideshowService.UpdateSettingsAsync(mappedSettings, cancellationToken);
        return Ok();
    }

    private ImageDataDto[] GetUploadedImages()
    {
        var content = _webHostEnvironment.WebRootFileProvider.GetDirectoryContents(
            _slideshowSettings.UploadedPhotosDirectory);

        return content.Select(x => new ImageDataDto
            { Path = Path.Combine(_slideshowSettings.UploadedPhotosDirectory, x.Name), FileName = x.Name }).ToArray();
    }
}