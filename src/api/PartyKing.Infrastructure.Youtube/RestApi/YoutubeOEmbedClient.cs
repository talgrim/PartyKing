using PartyKing.Application.Ports;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PartyKing.Domain.Entities.Youtube;
using PartyKing.Domain.Results;

namespace PartyKing.Infrastructure.Youtube.RestApi;

public sealed class YoutubeOEmbedClient : IYoutubeOEmbedClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<YoutubeOEmbedClient> _logger;

    public YoutubeOEmbedClient(HttpClient httpClient, ILogger<YoutubeOEmbedClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<YoutubeVideoLookupResult> GetVideoAsync(string videoId, CancellationToken cancellationToken)
    {
        var watchUrl = Uri.EscapeDataString($"https://www.youtube.com/watch?v={videoId}");

        try
        {
            using var response = await _httpClient.GetAsync(
                $"oembed?format=json&url={watchUrl}", cancellationToken);

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    var body = await response.Content.ReadFromJsonAsync<OEmbedResponse>(cancellationToken);
                    return body?.Title is null
                        ? new YoutubeVideoLookupResult(YoutubeVideoStatus.Unavailable)
                        : new YoutubeVideoLookupResult(
                            YoutubeVideoStatus.Found,
                            new YoutubeVideo(body.Title, body.AuthorName ?? string.Empty, body.ThumbnailUrl));

                // 400 and 404: no such video (or a private one). 401 and 403: the owner disabled embedding.
                case HttpStatusCode.BadRequest or HttpStatusCode.NotFound:
                    return new YoutubeVideoLookupResult(YoutubeVideoStatus.NotFound);

                case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                    return new YoutubeVideoLookupResult(YoutubeVideoStatus.NotEmbeddable);

                default:
                    _logger.LogWarning(
                        "YouTube oEmbed answered {StatusCode} for video {VideoId}", (int)response.StatusCode, videoId);
                    return new YoutubeVideoLookupResult(YoutubeVideoStatus.Unavailable);
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException
                                      && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(e, "Could not look up YouTube video {VideoId}", videoId);
            return new YoutubeVideoLookupResult(YoutubeVideoStatus.Unavailable);
        }
    }

    private sealed record OEmbedResponse(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("author_name")] string? AuthorName,
        [property: JsonPropertyName("thumbnail_url")] string? ThumbnailUrl);
}
