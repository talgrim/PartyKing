namespace PartyKing.Application.Configuration;

public class SlideshowConfiguration
{
    internal const string SectionName = "SlideshowSettings";

    public required string UploadedPhotosDirectory { get; set; }
    public required string PlaceholderPhotosDirectory { get; set; }
    public required TimeSpan SlideTime { get; set; }
}