namespace PartyKing.Domain.Models.Slideshow;

public class SlideshowImage
{
    public Guid Id { get; set; }
    public byte[] ImageData { get; set; } = null!;
    public string ImageUrl { get; set; } = null!;
    public string ContentType { get; set; } = null!;
}