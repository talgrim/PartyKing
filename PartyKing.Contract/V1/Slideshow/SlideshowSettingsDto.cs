namespace PartyKing.Contract.V1.Slideshow;

public class SlideshowSettingsDto
{
    public bool AutoRepeat { get; set; }
    public bool StartFromBeginning { get; set; }
    public TimeSpan? SlideTime { get; set; }
}