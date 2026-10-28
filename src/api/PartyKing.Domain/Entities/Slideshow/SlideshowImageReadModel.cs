namespace PartyKing.Domain.Entities.Slideshow;

public record SlideshowImageReadModel(
    string ImageName,
    string ImageUrl,
    string ContentType,
    bool DeleteAfterPresentation);
