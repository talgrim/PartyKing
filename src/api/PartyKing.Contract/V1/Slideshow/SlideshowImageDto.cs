namespace PartyKing.Contract.V1.Slideshow;

public class SlideshowImageDto
{
    public required string FileName { get; set; }
    public required DateTimeOffset ExpirationDate { get; set; }
}