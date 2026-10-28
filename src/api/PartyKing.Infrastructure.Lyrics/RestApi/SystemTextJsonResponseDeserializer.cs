using System.Text.Json;
using RestEase;

namespace PartyKing.Infrastructure.Lyrics.RestApi;

/// <summary>RestEase deserializes with Newtonsoft by default; the rest of the API uses System.Text.Json.</summary>
public sealed class SystemTextJsonResponseDeserializer : ResponseDeserializer
{
    public override T Deserialize<T>(string? content, HttpResponseMessage response, ResponseDeserializerInfo info) =>
        string.IsNullOrWhiteSpace(content)
            ? default!
            : JsonSerializer.Deserialize<T>(content, JsonSerializerOptions.Web)!;
}
