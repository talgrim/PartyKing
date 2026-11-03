using System.Net;

namespace PartyKing.Application.Ports;

/// <summary>Spotify's Web API or accounts service refused a request; <see cref="StatusCode"/> is the HTTP status it answered with, when known.</summary>
public class SpotifyApiException : Exception
{
    public SpotifyApiException(HttpStatusCode? statusCode, string message, Exception inner)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}
