namespace PartyKing.Contract.V1.Slideshow;

public class SlideshowImageDto
{
    public required string ImageName { get; set; }
    public required string ImageUrl { get; set; }
    public required string ContentType { get; set; }
    public required string FileName { get; set; }
    public required bool DeleteAfterPresentation { get; set; }
}