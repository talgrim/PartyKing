namespace PartyKing.Domain.Models.Slideshow;

public record SlideshowImageWriteModel(string Extension, string ContentType, Stream Data, string Path, bool DeleteAfterPresentation)
{
    public string GetFullPath(string imageName) => System.IO.Path.Combine(Path, imageName);
}