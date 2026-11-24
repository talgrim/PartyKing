using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Ports;
using PartyKing.Infrastructure.Mail;

namespace PartyKing.Application.Tests;

public class ResendMailClientTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly MailMessage Message = new("partyking@talgrim.dev", "A subject", "A body");

    private static (ResendMailClient Client, FakeHandler Handler) CreateClient(
        HttpStatusCode code = HttpStatusCode.OK,
        string body = """{"id":"abc"}""",
        string? replyTo = null)
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(code) { Content = new StringContent(body) });
        var configuration = new MailConfiguration { Sender = "partyking-requests@talgrim.dev", ReplyTo = replyTo };
        var client = new ResendMailClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") },
            Options.Create(configuration));

        return (client, handler);
    }

    [Fact]
    public async Task Send_PostsTheMailToTheEmailsEndpoint_FromTheConfiguredSender()
    {
        var (client, handler) = CreateClient();

        await client.SendAsync(Message, Ct);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.resend.com/emails", request.RequestUri?.ToString());
        using var json = JsonDocument.Parse(handler.Bodies.Single());
        var root = json.RootElement;
        Assert.Equal("partyking-requests@talgrim.dev", root.GetProperty("from").GetString());
        Assert.Equal(["partyking@talgrim.dev"], root.GetProperty("to").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("A subject", root.GetProperty("subject").GetString());
        Assert.Equal("A body", root.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Send_WithoutAReplyTo_LeavesTheFieldOut()
    {
        var (client, handler) = CreateClient();

        await client.SendAsync(Message, Ct);

        using var json = JsonDocument.Parse(handler.Bodies.Single());
        Assert.False(json.RootElement.TryGetProperty("reply_to", out _));
    }

    [Fact]
    public async Task Send_WithAReplyTo_SendsIt()
    {
        var (client, handler) = CreateClient(replyTo: "partyking@talgrim.dev");

        await client.SendAsync(Message, Ct);

        using var json = JsonDocument.Parse(handler.Bodies.Single());
        Assert.Equal("partyking@talgrim.dev", json.RootElement.GetProperty("reply_to").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, """{"name":"validation_error","message":"The talgrim.dev domain is not verified."}""")]
    [InlineData(HttpStatusCode.Unauthorized, """{"name":"restricted_api_key","message":"API key is invalid"}""")]
    [InlineData(HttpStatusCode.TooManyRequests, """{"name":"rate_limit_exceeded","message":"Too many requests"}""")]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    public async Task Send_WhenResendRefuses_Throws_WithTheStatusAndTheReason(HttpStatusCode code, string body)
    {
        var (client, _) = CreateClient(code, body);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(Message, Ct));

        Assert.Equal(code, exception.StatusCode);
        Assert.Contains($"{(int)code}", exception.Message);
        Assert.Contains(body, exception.Message);
    }

    [Fact]
    public async Task Send_WhenResendRefuses_DoesNotRepeatTheMailInTheError()
    {
        var (client, _) = CreateClient(HttpStatusCode.BadRequest, """{"message":"bad"}""");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(Message, Ct));

        Assert.DoesNotContain("A body", exception.Message);
    }

    [Fact]
    public async Task Send_WithAVeryLongErrorBody_KeepsTheErrorShort()
    {
        var (client, _) = CreateClient(HttpStatusCode.BadGateway, new string('x', 5000));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(Message, Ct));

        Assert.True(exception.Message.Length < 700);
    }

    [Fact]
    public async Task Send_WhenTheNetworkFails_Throws()
    {
        var client = new ResendMailClient(
            new HttpClient(new FakeHandler(_ => throw new HttpRequestException("offline")))
            {
                BaseAddress = new Uri("https://api.resend.com/")
            },
            Options.Create(new MailConfiguration { Sender = "partyking-requests@talgrim.dev" }));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(Message, Ct));
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));

            return respond(request);
        }
    }
}
