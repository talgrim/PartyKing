namespace PartyKing.Domain.Entities.Slideshow;

public class SlideshowImage
{
    private SlideshowImage()
    {
    }

    public SlideshowImage(SlideshowImageWriteModel writeModel)
    {
        SessionId = writeModel.SessionId;
        ImageName = $"{Id}{writeModel.Extension}";
        ImageUrl = writeModel.GetFullPath(ImageName);
        ContentType = writeModel.ContentType;
        DeleteAfterPresentation = writeModel.DeleteAfterPresentation;
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