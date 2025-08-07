namespace PartyKing.Domain.Models.Slideshow;

public record SlideshowImageWriteModel(string ImageUrl, string ContentType, Stream Data, string RootPath)
{
    public string GetFullPath() => Path.Combine(RootPath, ImageUrl);
}