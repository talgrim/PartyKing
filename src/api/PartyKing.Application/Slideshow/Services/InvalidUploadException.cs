namespace PartyKing.Application.Slideshow.Services;

/// <summary>Thrown for an upload that is not an acceptable image; the message is safe to show to the client.</summary>
public class InvalidUploadException(string message) : Exception(message);
