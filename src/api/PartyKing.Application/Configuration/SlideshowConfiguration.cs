namespace PartyKing.Application.Configuration;

public class SlideshowConfiguration
{
    internal const string SectionName = "SlideshowSettings";

    /// <summary>Directory under the web root; every session's uploads go to a subdirectory named after its id.</summary>
    public required string UploadedPhotosDirectory { get; set; }

    /// <summary>Slide time of a session that has not set its own.</summary>
    public required TimeSpan SlideTime { get; set; }
}
