namespace PartyKing.Domain.Models.Slideshow;

public class SlideshowImageWriteModel
{
    public string ImageUrl { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public Stream Data { get; set; } = null!;
}