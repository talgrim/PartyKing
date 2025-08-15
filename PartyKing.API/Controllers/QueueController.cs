using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Contract.V1.Enums;
using PartyKing.Contract.V1.Queue;

namespace PartyKing.API.Controllers;

public class QueueController : CoreController
{
    private bool _switch = false;

    private readonly (string Url, string Name)[] _youtubeUrls =
    [
        ("https://music.youtube.com/watch?v=nDR-Ex-tATU", "Amazing Horse Extended Version Animation Video"),
        ("https://www.youtube.com/watch?v=wbby9coDRCk", "Narwhals"),
        ("https://www.youtube.com/watch?v=zWq65etOM-M", "You are a Pirate Limewire")
    ];

    public QueueController(
        IHttpContextAccessor httpContextAccessor,
        IOptions<SlideshowConfiguration> slideshowSettingsOptions,
        IWebHostEnvironment webHostEnvironment)
        : base(httpContextAccessor, slideshowSettingsOptions, webHostEnvironment)
    {
    }

    [HttpGet("queue-song")]
    [ProducesResponseType<SongDataDto>(StatusCodes.Status200OK)]
    public Task<IActionResult> QueueSong(string url, CancellationToken cancellationToken)
    {
        return Task.FromResult<IActionResult>(Ok());
    }

    [HttpGet("get-next")]
    [ProducesResponseType<SongDataDto>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetNext()
    {
        return Task.FromResult<IActionResult>(Ok(GenerateRandom()));
    }

    private SongDataDto GenerateRandom()
    {
        _switch = !_switch;
        if (_switch)
        {
            var (url, name) = _youtubeUrls[Random.Shared.Next(_youtubeUrls.Length)];
            return new SongDataDto(SongSource.Youtube, url, name);
        }

        return SongDataDto.Spotify("Mock name");
    }
}