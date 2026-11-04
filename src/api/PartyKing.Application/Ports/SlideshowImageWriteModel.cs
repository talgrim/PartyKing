namespace PartyKing.Application.Ports;

/// <summary>An uploaded image on its way to the repository, which stores the content and the row.</summary>
/// <param name="Path">Directory of the image relative to the web root, using '/' so it works as a URL as well.</param>
public record SlideshowImageWriteModel(
    Guid SessionId,
    string Extension,
    string ContentType,
    Stream Data,
    string Path,
    bool DeleteAfterPresentation);
