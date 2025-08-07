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
    }

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string ImageUrl { get; set; }
    public string ContentType { get; set; }

    public SlideshowImageReadModel ToReadModel()
    {
        return new SlideshowImageReadModel
        {
            ImageUrl = ImageUrl,
            ContentType = ContentType
        };
    }
}