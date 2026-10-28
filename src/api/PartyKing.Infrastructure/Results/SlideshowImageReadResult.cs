using PartyKing.Domain.Entities.Slideshow;

namespace PartyKing.Infrastructure.Results;

public record SlideshowImageReadResult(Guid? ImageId, SlideshowImageReadModel? Data)
{
    public bool IsOk => Data is not null;
}