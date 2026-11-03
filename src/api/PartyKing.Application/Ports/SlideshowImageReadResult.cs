using PartyKing.Domain.Entities.Slideshow;

namespace PartyKing.Application.Ports;

public record SlideshowImageReadResult(Guid? ImageId, SlideshowImageReadModel? Data)
{
    public bool IsOk => Data is not null;
}