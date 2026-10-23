using PartyKing.Application.Sessions.Helpers;

namespace PartyKing.Application.Tests;

public class SessionCodeGeneratorTests
{
    [Fact]
    public void Generate_ReturnsCodeOfExpectedLength()
    {
        Assert.Equal(SessionCodeGenerator.CodeLength, SessionCodeGenerator.Generate().Length);
    }

    [Fact]
    public void Generate_UsesOnlyUnambiguousUppercaseCharacters()
    {
        for (var i = 0; i < 500; i++)
        {
            Assert.All(SessionCodeGenerator.Generate(), c => Assert.Contains(c, SessionCodeGenerator.Alphabet));
        }
    }

    [Theory]
    [InlineData('0')]
    [InlineData('O')]
    [InlineData('1')]
    [InlineData('I')]
    public void Alphabet_ExcludesLookAlikeCharacters(char character)
    {
        Assert.DoesNotContain(character, SessionCodeGenerator.Alphabet);
    }

    [Fact]
    public void Generate_ProducesVariedCodes()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => SessionCodeGenerator.Generate()).ToHashSet();

        Assert.True(codes.Count > 190);
    }
}
