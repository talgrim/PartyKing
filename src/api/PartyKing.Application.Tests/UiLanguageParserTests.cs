using PartyKing.Application.Localization;

namespace PartyKing.Application.Tests;

public class UiLanguageParserTests
{
    [Theory]
    [InlineData(null, UiLanguage.En)]
    [InlineData("", UiLanguage.En)]
    [InlineData("   ", UiLanguage.En)]
    [InlineData("en", UiLanguage.En)]
    [InlineData("pl", UiLanguage.Pl)]
    [InlineData("PL", UiLanguage.Pl)]
    [InlineData("pl-PL", UiLanguage.Pl)]
    [InlineData("en-GB", UiLanguage.En)]
    public void ASupportedLanguage_IsRecognised(string? header, UiLanguage expected) =>
        Assert.Equal(expected, UiLanguageParser.Parse(header));

    [Theory]
    [InlineData("de", UiLanguage.En)]
    [InlineData("*", UiLanguage.En)]
    [InlineData("plx", UiLanguage.En)]
    [InlineData("de, fr;q=0.8", UiLanguage.En)]
    [InlineData("de, pl;q=0.5", UiLanguage.Pl)]
    public void AnUnsupportedLanguage_IsSkipped_AndEnglishIsTheDefault(string header, UiLanguage expected) =>
        Assert.Equal(expected, UiLanguageParser.Parse(header));

    [Theory]
    [InlineData("pl;q=0.9, en;q=0.8", UiLanguage.Pl)]
    [InlineData("en;q=0.8, pl;q=0.9", UiLanguage.Pl)]
    [InlineData("en;q=0.5, pl", UiLanguage.Pl)]
    [InlineData("pl, en", UiLanguage.Pl)]
    [InlineData("en, pl", UiLanguage.En)]
    [InlineData("pl;q=0, en;q=0.1", UiLanguage.En)]
    [InlineData("pl;q=0", UiLanguage.En)]
    [InlineData("pl;q=abc, en;q=0.1", UiLanguage.En)]
    [InlineData("pl-PL,pl;q=0.9,en-US;q=0.8,en;q=0.7", UiLanguage.Pl)]
    public void TheHighestQuality_Wins_AndTheFirstOnATie(string header, UiLanguage expected) =>
        Assert.Equal(expected, UiLanguageParser.Parse(header));

    [Fact]
    public void ALongHeader_OnlyHasItsFirstEntriesRead()
    {
        var header = string.Join(", ", Enumerable.Repeat("de", 30)) + ", pl";

        Assert.Equal(UiLanguage.En, UiLanguageParser.Parse(header));
    }
}
