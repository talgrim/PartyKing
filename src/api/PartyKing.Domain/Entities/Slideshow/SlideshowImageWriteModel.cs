namespace PartyKing.Domain.Entities.Slideshow;

/// <param name="Path">Directory of the image relative to the web root, using '/' so it works as a URL as well.</param>
public record SlideshowImageWriteModel(
    Guid SessionId,
    string Extension,
    string ContentType,
    Stream Data,
    string Path,
    bool DeleteAfterPresentation)
{
    public string GetFullPath(string imageName) => $"{Path}/{imageName}";
}
