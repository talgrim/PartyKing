using PartyKing.Application.Games.Services.Resistance;

namespace PartyKing.Application.Tests;

/// <summary>The errors every Resistance action shares. The ones by failure are pinned by <see cref="ResistanceFailureErrorsTests"/>.</summary>
public class ResistanceErrorsTests
{
    [Fact]
    public void SharedErrors_HaveStableCodes()
    {
        Assert.Equal(ErrorOr.ErrorType.Conflict, ResistanceErrors.AlreadyCreated.Type);
        Assert.Equal("Resistance.AlreadyCreated", ResistanceErrors.AlreadyCreated.Code);
        Assert.Equal("The session already has a Resistance game.", ResistanceErrors.AlreadyCreated.Description);
    }
}
