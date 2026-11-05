using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games.Resistance;
using Riok.Mapperly.Abstractions;

namespace PartyKing.Application.Mappings;

/// <summary>Domain to Contract enums by member name, so a renamed or missing member fails the build instead of shifting values.</summary>
[Mapper(EnumMappingStrategy = EnumMappingStrategy.ByName)]
internal static partial class ResistanceEnumMappings
{
    public static partial ResistanceRoleDto ToDto(this ResistanceRole role);

    public static partial ResistancePhaseDto ToDto(this ResistancePhase phase);

    public static partial ResistancePlotCardTypeDto ToDto(this ResistancePlotCardType type);

    public static partial ResistanceCharacterDto ToDto(this ResistanceCharacter character);

    public static partial ResistanceMissionCardKindDto ToDto(this ResistanceMissionCardKind card);

    public static partial ResistanceReverserSidesDto ToDto(this ResistanceReverserSides sides);

    public static partial ResistanceReverserSides ToDomain(this ResistanceReverserSidesDto sides);

    public static partial ResistanceMissionCardKind ToDomain(this ResistanceMissionCardKindDto card);
}
