namespace PartyKing.Domain.Models.Slideshow;

public record SlideshowImageReadModel(
    string ImageName,
    string ImageUrl,
    string ContentType,
    bool DeleteAfterPresentation);
