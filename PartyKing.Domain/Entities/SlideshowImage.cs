using PartyKing.Domain.Enums;
using PartyKing.Domain.Models.Slideshow;

namespace PartyKing.Domain.Entities;

public class SlideshowImage
{
    private SlideshowImage()
    {
    }

    public SlideshowImage(SlideshowImageWriteModel writeModel)
    {
        ImageUrl = writeModel.ImageUrl;
        ContentType = writeModel.ContentType;
        DeleteAfterPresentation = writeModel.DeleteAfterPresentation;
    }

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string ImageName { get; set; }
    public string ImageUrl { get; set; }
    public string ContentType { get; set; }
    public bool DeleteAfterPresentation { get; set; }

    public SlideshowImageReadModel ToReadModel(SlideshowImageSource source)
    {
        return new SlideshowImageReadModel(ImageName, ImageUrl, ContentType, source, DeleteAfterPresentation);
    }
}