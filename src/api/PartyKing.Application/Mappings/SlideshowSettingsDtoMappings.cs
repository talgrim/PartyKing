using PartyKing.Contract.V1.Slideshow;
using PartyKing.Domain.Entities.Slideshow;
using Riok.Mapperly.Abstractions;

namespace PartyKing.Application.Mappings;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class SlideshowSettingsDtoMappings
{
    public static SlideshowSettings ToDomain(this SlideshowSettingsDto dto, Guid sessionId, TimeSpan defaultSlideTime)
    {
        var settings = Map(dto, sessionId);
        settings.SlideTime = dto.SlideTime ?? defaultSlideTime;
        return settings;
    }

    public static partial SlideshowSettingsDto ToDto(this SlideshowSettings settings);

    // SlideTime is optional in the DTO, so ToDomain fills it with the default.
    [MapperIgnoreTarget(nameof(SlideshowSettings.SlideTime))]
    private static partial SlideshowSettings Map(SlideshowSettingsDto dto, Guid sessionId);
}
