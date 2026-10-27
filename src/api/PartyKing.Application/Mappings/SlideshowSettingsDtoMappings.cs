using PartyKing.Contract.V1.Slideshow;
using PartyKing.Domain.Entities.Slideshow;

namespace PartyKing.Application.Mappings;

public static class SlideshowSettingsDtoMappings
{
    public static SlideshowSettings ToDomain(this SlideshowSettingsDto dto, Guid sessionId, TimeSpan defaultSlideTime)
    {
        return new SlideshowSettings
        {
            SessionId = sessionId,
            AutoRepeat = dto.AutoRepeat,
            StartFromBeginning = dto.StartFromBeginning,
            SlideTime = dto.SlideTime ?? defaultSlideTime,
        };
    }

    public static SlideshowSettingsDto ToDto(this SlideshowSettings settings)
    {
        return new SlideshowSettingsDto
        {
            AutoRepeat = settings.AutoRepeat,
            StartFromBeginning = settings.StartFromBeginning,
            SlideTime = settings.SlideTime,
        };
    }
}
