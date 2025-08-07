using PartyKing.Domain.Enums;

namespace PartyKing.Domain.Models.Slideshow;

public record SlideshowImageReadModel(string ImageUrl, string ContentType, SlideshowImageSource ImageSource);