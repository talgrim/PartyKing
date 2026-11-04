namespace PartyKing.Domain.Entities.Slideshow;

public class SlideshowImage
{
    private SlideshowImage()
    {
    }

    /// <param name="directory">Directory of the image relative to the web root, using '/' so it works as a URL as well.</param>
    public SlideshowImage(
        Guid sessionId,
        string extension,
        string contentType,
        string directory,
        bool deleteAfterPresentation)
    {
        SessionId = sessionId;
        ImageName = $"{Id}{extension}";
        ImageUrl = $"{directory}/{ImageName}";
        ContentType = contentType;
        DeleteAfterPresentation = deleteAfterPresentation;
    }

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SessionId { get; set; }
    public string ImageName { get; set; }
    public string ImageUrl { get; set; }
    public string ContentType { get; set; }
    public bool DeleteAfterPresentation { get; set; }
    public bool WasPresented { get; set; }

    public SlideshowImageReadModel ToReadModel()
    {
        return new SlideshowImageReadModel(ImageName, ImageUrl, ContentType, DeleteAfterPresentation);
    }
}