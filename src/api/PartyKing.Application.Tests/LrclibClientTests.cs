using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Lyrics.RestApi;
using RestEase;

namespace PartyKing.Application.Tests;

public class LrclibClientTests
{
    private const string Body = """
        [
          {"trackName":"A song","artistName":"An artist","duration":201.0,"instrumental":false,
           "plainLyrics":"x","syncedLyrics":"[00:01.00]Hello\n[00:05.00]World"},
          {"trackName":"Plain only","artistName":"Someone","duration":100,"instrumental":false,"syncedLyrics":null},
          {"trackName":"Tune","artistName":"Band","duration":90.5,"instrumental":true,"syncedLyrics":null}
        ]
        """;

    [Fact]
    public async Task SearchAsync_ReturnsTheCandidatesWithTheirSyncedLines()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, Body);

        var result = await client.SearchAsync("A song", "An artist", null, CancellationToken.None);

        Assert.Equal(LyricsSearchStatus.Completed, result.Status);
        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal(201, result.Candidates[0].DurationSeconds);
        Assert.Equal(["Hello", "World"], result.Candidates[0].SyncedLines.Select(l => l.Text));
        Assert.Empty(result.Candidates[1].SyncedLines);
        Assert.True(result.Candidates[2].Instrumental);
        Assert.Equal("/api/search?track_name=A+song&artist_name=An+artist", handler.Requests.Single().RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task SearchAsync_WithAQuery_UsesTheFreeTextSearch()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, "[]");

        await client.SearchAsync("ignored", "ignored", "artist & song", CancellationToken.None);

        Assert.Equal("/api/search?q=artist+%26+song", handler.Requests.Single().RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task SearchAsync_NothingFound_IsCompletedWithoutCandidates()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, "[]");

        var result = await client.SearchAsync("x", null, null, CancellationToken.None);

        Assert.Equal(LyricsSearchStatus.Completed, result.Status);
        Assert.Empty(result.Candidates);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task SearchAsync_ErrorStatus_IsUnavailable(HttpStatusCode code)
    {
        var (client, _) = CreateClient(code);

        var result = await client.SearchAsync("x", null, null, CancellationToken.None);

        Assert.Equal(LyricsSearchStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task SearchAsync_UnreadableBody_IsUnavailable()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, "not json");

        Assert.Equal(LyricsSearchStatus.Unavailable, (await client.SearchAsync("x", null, null, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task SearchAsync_WhenTheServiceCannotBeReached_IsUnavailable()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("offline"));
        var client = CreateClient(handler);

        Assert.Equal(LyricsSearchStatus.Unavailable, (await client.SearchAsync("x", null, null, CancellationToken.None)).Status);
    }

    private static (LrclibClient Client, FakeHandler Handler) CreateClient(HttpStatusCode code, string? body = null)
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(code)
        {
            Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json")
        });

        return (CreateClient(handler), handler);
    }

    private static LrclibClient CreateClient(FakeHandler handler)
    {
        var api = new RestClient(new HttpClient(handler) { BaseAddress = new Uri("https://lrclib.test/") })
        {
            ResponseDeserializer = new SystemTextJsonResponseDeserializer()
        }.For<ILrclibApi>();

        return new LrclibClient(api, NullLogger<LrclibClient>.Instance);
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
