using PartyKing.Contract.V1.Slideshow;
using PartyKing.Domain.Entities.Slideshow;
using Riok.Mapperly.Abstractions;

namespace PartyKing.Application.Mappings;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class SlideshowSettingsDtoMappings
{
    public static partial SlideshowSettingsDto ToDto(this SlideshowSettings settings);
}
