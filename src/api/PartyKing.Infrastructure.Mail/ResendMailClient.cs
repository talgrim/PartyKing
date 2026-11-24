using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Ports;

namespace PartyKing.Infrastructure.Mail;

/// <summary>
/// Sends mail through Resend's HTTP API (<c>POST /emails</c>). The only class that knows Resend: another provider
/// means rewriting this one. The key, base address and headers are set on the <see cref="HttpClient"/> by
/// <see cref="DependencyInjection"/>.
/// </summary>
internal sealed class ResendMailClient : IMailClient
{
    private const int MaxErrorBodyLength = 500;

    private readonly HttpClient _httpClient;
    private readonly MailConfiguration _configuration;

    public ResendMailClient(HttpClient httpClient, IOptions<MailConfiguration> configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration.Value;
    }

    public async Task SendAsync(MailMessage message, CancellationToken cancellationToken)
    {
        var request = new ResendEmailRequest(
            _configuration.Sender!,
            [message.To],
            message.Subject,
            message.Body,
            string.IsNullOrWhiteSpace(_configuration.ReplyTo) ? null : _configuration.ReplyTo);

        using var response = await _httpClient.PostAsJsonAsync("emails", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // Resend explains a refusal in its body (an unverified domain, a bad key); the mail itself is not repeated in the error
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var detail = body.Length > MaxErrorBodyLength ? body[..MaxErrorBodyLength] : body;

        throw new HttpRequestException(
            $"Resend refused the mail with {(int)response.StatusCode}: {detail}",
            inner: null,
            response.StatusCode);
    }

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("reply_to"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ReplyTo);
}
