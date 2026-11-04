namespace PartyKing.Domain.Entities.Slideshow;

/// <summary>One uploaded photo of a session. What is stored never changes, except that it is marked as presented.</summary>
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

    public Guid Id { get; internal set; } = Guid.CreateVersion7();
    public Guid SessionId { get; internal set; }
    public string ImageName { get; internal set; }
    public string ImageUrl { get; internal set; }
    public string ContentType { get; internal set; }
    public bool DeleteAfterPresentation { get; internal set; }
    public bool WasPresented { get; internal set; }

    /// <summary>The image has been on display and left it. Only a <see cref="SlideshowPlaylist"/> says so.</summary>
    internal void MarkPresented()
    {
        WasPresented = true;
    }

    public SlideshowImageReadModel ToReadModel()
    {
        return new SlideshowImageReadModel(ImageName, ImageUrl, ContentType, DeleteAfterPresentation);
    }
}