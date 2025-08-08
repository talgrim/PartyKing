using PartyKing.Domain.Enums;

namespace PartyKing.Domain.Models.Slideshow;

public record SlideshowImageReadModel(
    string ImageName,
    string ImageUrl,
    string ContentType,
    SlideshowImageSource ImageSource,
    bool DeleteAfterPresentation)
{
    public static SlideshowImageReadModel Placeholder(string imageName, string imageUrl, string contentType)
    {
        return new SlideshowImageReadModel(imageName, imageUrl, contentType, SlideshowImageSource.Placeholder, false);
    }
}