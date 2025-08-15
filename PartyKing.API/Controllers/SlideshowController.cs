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

        if (!_slideshowService.IsInitialized())
        {
            _ = _slideshowService.InitializeAsync(GetPhysicalRoot(), CancellationToken.None);
        }
    }

    [HttpPut("upload-photo")]
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
        return Ok(_slideshowSettings);
    }

    [HttpPost("update-configuration")]
    public async Task<IActionResult> UpdateConfiguration(
        [FromBody] SlideshowSettingsDto settings,
        CancellationToken cancellationToken)
    {
        var mappedSettings = settings.ToDomain(_slideshowSettings.SlideTime);
        await _slideshowService.UpdateSettingsAsync(mappedSettings, cancellationToken);
        return Ok();
    }

    private string GetPhysicalRoot()
    {
        var result = _webHostEnvironment.WebRootPath;
        if (!Directory.Exists(result))
        {
            Directory.CreateDirectory(result);
        }

        return result;
    }

    private ImageDataDto[] GetUploadedImages()
    {
        var content = _webHostEnvironment.WebRootFileProvider.GetDirectoryContents(
            _slideshowSettings.UploadedPhotosDirectory);

        return content.Select(x => new ImageDataDto
            { Path = Path.Combine(_slideshowSettings.UploadedPhotosDirectory, x.Name), FileName = x.Name }).ToArray();
    }
}