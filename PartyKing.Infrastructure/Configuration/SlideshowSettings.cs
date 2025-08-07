namespace PartyKing.Infrastructure.Configuration;

internal class SlideshowSettings
{
    internal const string SectionName = "SlideshowSettings";

    public required string UploadedPhotosDirectory { get; set; }
    public required string PlaceholderPhotosDirectory { get; set; }
}