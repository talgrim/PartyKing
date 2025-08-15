namespace PartyKing.Domain.Entities;

public class SlideshowSettings
{
    public int Id { get; set; } = 1;
    public bool AutoRepeat { get; set; }
    public TimeSpan SlideTime { get; set; }
    public bool StartFromBeginning { get; set; }
}