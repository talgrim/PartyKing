using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Mappings;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Contract.V1.Slideshow;

namespace PartyKing.API.Controllers;

public class SlideshowController : CoreController
{
    private readonly ISlideshowService _slideshowService;
    private readonly ILogger<SlideshowController> _logger;

    public SlideshowController(
        IHttpContextAccessor httpContextAccessor,
        ISlideshowService slideshowService,
        IOptions<SlideshowConfiguration> slideshowSettingsOptions,
        IWebHostEnvironment webHostEnvironment,
        ILogger<SlideshowController> logger)
        : base(httpContextAccessor, slideshowSettingsOptions, webHostEnvironment)
    {
        _slideshowService = slideshowService;
        _logger = logger;

        if (!_slideshowService.IsInitialized())
        {
            _ = _slideshowService.InitializeAsync(GetPhysicalRoot(), CancellationToken.None);
        }
    }

    [HttpPut("upload-photo/{deleteAfterPresentation:bool}")]
    public async Task<IActionResult> UploadPhoto([Required] IFormFile[] files, bool deleteAfterPresentation, CancellationToken cancellationToken)
    {
        try
        {
            await _slideshowService.UploadImagesAsync(files, deleteAfterPresentation, cancellationToken);
            return Ok();
        }
        catch (Exception e)
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
    [ProducesResponseType<IEnumerable<ImageDataDto>>(StatusCodes.Status200OK)]
    public IActionResult GetAll()
    {
        return Ok(GetUploadedImages());
    }

    [HttpGet("get-configuration")]
    [ProducesResponseType<SlideshowConfiguration>(StatusCodes.Status200OK)]
    public IActionResult GetConfiguration()
    {
        return Ok(SlideshowConfiguration);
    }

    [HttpPost("update-configuration")]
    public async Task<IActionResult> UpdateConfiguration(
        [FromBody] SlideshowSettingsDto settings,
        CancellationToken cancellationToken)
    {
        var mappedSettings = settings.ToDomain(SlideshowConfiguration.SlideTime);
        await _slideshowService.UpdateSettingsAsync(mappedSettings, cancellationToken);
        return Ok();
    }
}