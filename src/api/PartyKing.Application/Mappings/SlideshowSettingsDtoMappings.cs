using PartyKing.Contract.V1.Slideshow;
using PartyKing.Domain.Entities;

namespace PartyKing.Application.Mappings;

public static class SlideshowSettingsDtoMappings
{
    public static SlideshowSettings ToDomain(this SlideshowSettingsDto dto, TimeSpan defaultSlideTime)
    {
        return new SlideshowSettings
        {
            AutoRepeat = dto.AutoRepeat,
            StartFromBeginning = dto.StartFromBeginning,
            SlideTime = dto.SlideTime ?? defaultSlideTime,
        };
    }
}