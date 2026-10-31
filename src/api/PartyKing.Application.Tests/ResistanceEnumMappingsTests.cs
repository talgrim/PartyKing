using PartyKing.Application.Mappings;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Tests;

public class ResistanceEnumMappingsTests
{
    [Fact]
    public void Role_MapsEveryMemberByName()
    {
        foreach (var value in Enum.GetValues<ResistanceRole>())
            Assert.Equal(value.ToString(), value.ToDto().ToString());
    }

    [Fact]
    public void Phase_MapsEveryMemberByName()
    {
        foreach (var value in Enum.GetValues<ResistancePhase>())
            Assert.Equal(value.ToString(), value.ToDto().ToString());
    }

    [Fact]
    public void PlotCardType_MapsEveryMemberByName()
    {
        foreach (var value in Enum.GetValues<ResistancePlotCardType>())
            Assert.Equal(value.ToString(), value.ToDto().ToString());
    }

    [Fact]
    public void ContractEnums_HaveNoMemberMissingFromDomain()
    {
        Assert.Equal(Enum.GetNames<ResistanceRole>().Length, Enum.GetNames<ResistanceRoleDto>().Length);
        Assert.Equal(Enum.GetNames<ResistancePhase>().Length, Enum.GetNames<ResistancePhaseDto>().Length);
        Assert.Equal(Enum.GetNames<ResistancePlotCardType>().Length, Enum.GetNames<ResistancePlotCardTypeDto>().Length);
    }
}
