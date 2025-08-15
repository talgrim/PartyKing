namespace PartyKing.Domain.Models.Slideshow;

public record SlideshowImageWriteModel(string ImageName, string ContentType, Stream Data, string Path, bool DeleteAfterPresentation)
{
    public string GetFullPath() => System.IO.Path.Combine(Path, ImageName);
}