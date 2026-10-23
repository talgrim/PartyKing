using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Youtube.RestApi;

namespace PartyKing.Application.Tests;

public class YoutubeOEmbedClientTests
{
    private const string VideoId = "dQw4w9WgXcQ";

    [Fact]
    public async Task GetVideoAsync_Ok_ReturnsTheVideo()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK,
            """{"title":"A song","author_name":"An artist","thumbnail_url":"https://i.ytimg.com/vi/x/hqdefault.jpg"}""");

        var result = await client.GetVideoAsync(VideoId, CancellationToken.None);

        Assert.Equal(YoutubeVideoStatus.Found, result.Status);
        Assert.Equal("A song", result.Video?.Title);
        Assert.Equal("An artist", result.Video?.AuthorName);
        Assert.Equal("https://i.ytimg.com/vi/x/hqdefault.jpg", result.Video?.ThumbnailUrl);
        Assert.Contains(
            Uri.EscapeDataString($"https://www.youtube.com/watch?v={VideoId}"),
            handler.Requests.Single().RequestUri?.Query);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, YoutubeVideoStatus.NotEmbeddable)]
    [InlineData(HttpStatusCode.Forbidden, YoutubeVideoStatus.NotEmbeddable)]
    [InlineData(HttpStatusCode.NotFound, YoutubeVideoStatus.NotFound)]
    [InlineData(HttpStatusCode.BadRequest, YoutubeVideoStatus.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError, YoutubeVideoStatus.Unavailable)]
    [InlineData(HttpStatusCode.TooManyRequests, YoutubeVideoStatus.Unavailable)]
    public async Task GetVideoAsync_ErrorStatus_IsMappedToAStatus(HttpStatusCode code, YoutubeVideoStatus expected)
    {
        var (client, _) = CreateClient(code);

        var result = await client.GetVideoAsync(VideoId, CancellationToken.None);

        Assert.Equal(expected, result.Status);
        Assert.Null(result.Video);
    }

    [Fact]
    public async Task GetVideoAsync_UnreadableBody_IsUnavailable()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, "not json");

        var result = await client.GetVideoAsync(VideoId, CancellationToken.None);

        Assert.Equal(YoutubeVideoStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task GetVideoAsync_NetworkFailure_IsUnavailable()
    {
        var client = new YoutubeOEmbedClient(
            new HttpClient(new FakeHandler(_ => throw new HttpRequestException("offline")))
            {
                BaseAddress = new Uri("https://www.youtube.com/")
            },
            NullLogger<YoutubeOEmbedClient>.Instance);

        var result = await client.GetVideoAsync(VideoId, CancellationToken.None);

        Assert.Equal(YoutubeVideoStatus.Unavailable, result.Status);
    }

    private static (YoutubeOEmbedClient Client, FakeHandler Handler) CreateClient(
        HttpStatusCode code,
        string body = "")
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(code) { Content = new StringContent(body) });
        var client = new YoutubeOEmbedClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://www.youtube.com/") },
            NullLogger<YoutubeOEmbedClient>.Instance);

        return (client, handler);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}
