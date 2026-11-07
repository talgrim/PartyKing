using System.Reflection;
using System.Text.Json;
using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Domain.Tests;

/// <summary>
/// What the JSON column of <see cref="ResistanceGame.Rules"/> relies on: the serialized shape (the default is baked
/// into the model snapshot and the migrations) and the deep copy that gives a changed game a new instance.
/// </summary>
public class ResistanceRulesStorageTests
{
    // The same string as the HasDefaultValue in the model snapshot. When this fails the column default moved, which
    // needs a migration (and a new line here); a refactor that leaves it alone must not.
    private const string DefaultJson =
        """
        {"PlayerCounts":{"5":{"Spies":2,"TeamSizes":[2,3,2,3,3],"RequiredFails":[1,1,1,1,1]},"6":{"Spies":2,"TeamSizes":[2,3,4,3,4],"RequiredFails":[1,1,1,1,1]},"7":{"Spies":3,"TeamSizes":[2,3,3,4,4],"RequiredFails":[1,1,1,2,1]},"8":{"Spies":3,"TeamSizes":[3,4,4,5,5],"RequiredFails":[1,1,1,2,1]},"9":{"Spies":3,"TeamSizes":[3,4,4,5,5],"RequiredFails":[1,1,1,2,1]},"10":{"Spies":4,"TeamSizes":[3,4,4,5,5],"RequiredFails":[1,1,1,2,1]}},"PlotCardsEnabled":false,"InquisitorEnabled":false,"BlindSpiesEnabled":false,"ReverserEnabled":false,"ReverserSides":0,"ReverserSpyKnowsSpies":true,"DefectorEnabled":false,"DefectorsKnowEachOther":false,"AssassinEnabled":false,"BodyGuardEnabled":false,"DeepCoverEnabled":false,"BlindSpyEnabled":false,"FalseCommanderEnabled":false,"OptionalSpyCharacters":0,"PlotDeck":{"OverheardConversation":2,"OpinionMaker":2,"EstablishConfidence":1,"OpenUp":1,"NoConfidence":3,"KeepingCloseEye":2,"StrongLeader":2,"InTheSpotlight":1,"TakeResponsibility":1}}
        """;

    private static string Serialize(ResistanceRules rules) => JsonSerializer.Serialize(rules, (JsonSerializerOptions?)null);

    private static ResistanceRules Deserialize(string json) =>
        JsonSerializer.Deserialize<ResistanceRules>(json, (JsonSerializerOptions?)null)!;

    /// <summary>Every settable property moved off its default, so a copy that forgets one shows up.</summary>
    private static ResistanceRules EveryPropertyChanged()
    {
        var rules = ResistanceRules.Default();

        foreach (var property in typeof(ResistanceRules).GetProperties().Where(x => x.SetMethod is { IsPublic: true }))
        {
            ChangeFromDefault(rules, property);
        }

        return rules;
    }

    private static void ChangeFromDefault(ResistanceRules rules, PropertyInfo property)
    {
        if (property.PropertyType == typeof(bool))
        {
            property.SetValue(rules, !(bool)property.GetValue(rules)!);
        }
        else if (property.PropertyType == typeof(ResistanceReverserSides))
        {
            property.SetValue(rules, ResistanceReverserSides.Spy);
        }
        else if (property.Name == nameof(ResistanceRules.PlayerCounts))
        {
            rules.PlayerCounts[5] = new ResistanceCountRules(1, [1, 1, 1, 1, 1], [2, 2, 2, 2, 2]);
        }
        else if (property.Name == nameof(ResistanceRules.PlotDeck))
        {
            rules.PlotDeck[ResistancePlotCardType.NoConfidence] = 7;
        }
        else
        {
            // A property of a new kind: teach this test how to move it off its default
            Assert.Fail($"{property.Name} ({property.PropertyType.Name}) is not covered by the copy test.");
        }
    }

    [Fact]
    public void TheDefaultRules_SerializeToTheStringInTheModelSnapshot()
    {
        Assert.Equal(DefaultJson, Serialize(ResistanceRules.Default()));
    }

    [Fact]
    public void TheDefaultRules_SurviveASaveAndALoad()
    {
        var loaded = Deserialize(Serialize(ResistanceRules.Default()));

        Assert.Equal(DefaultJson, Serialize(loaded));
    }

    [Fact]
    public void RulesSavedBeforeASettingExisted_LoadWithItsDefault()
    {
        // A row from before the Reverser, the Defectors and the Assassin: none of their properties is stored
        const string old =
            """
            {"PlayerCounts":{"5":{"Spies":2,"TeamSizes":[2,3,2,3,3],"RequiredFails":[1,1,1,1,1]}},"PlotCardsEnabled":true,"PlotDeck":{"NoConfidence":3}}
            """;

        var rules = Deserialize(old);

        Assert.True(rules.PlotCardsEnabled);
        Assert.True(rules.ReverserSpyKnowsSpies);
        Assert.False(rules.ReverserEnabled);
        Assert.False(rules.DefectorEnabled);
        Assert.False(rules.AssassinEnabled);
        Assert.Equal(ResistanceReverserSides.Both, rules.ReverserSides);
        Assert.Equal(2, rules.SpyCount(5));
        Assert.Equal(3, rules.PlotDeck[ResistancePlotCardType.NoConfidence]);
    }

    [Fact]
    public void ReverserSides_AreStoredByNumber_AndThePlotDeckByName()
    {
        var rules = ResistanceRules.Default();
        rules.ReverserSides = ResistanceReverserSides.Spy;

        var json = Serialize(rules);

        Assert.Contains("\"ReverserSides\":2", json);
        Assert.Contains("\"NoConfidence\":3", json);
    }

    [Fact]
    public void Clone_CopiesEverySettableProperty()
    {
        var rules = EveryPropertyChanged();

        var copy = rules.Clone();

        Assert.Equal(Serialize(rules), Serialize(copy));
        // The test would pass for a copy that forgot a property if nothing had moved
        Assert.NotEqual(DefaultJson, Serialize(copy));
    }

    [Fact]
    public void Clone_IsANewInstance_ThatSharesNothingWithTheOriginal()
    {
        var rules = ResistanceRules.Default();

        var copy = rules.Clone();
        copy.PlotDeck[ResistancePlotCardType.NoConfidence] = 9;
        copy.PlayerCounts[5].TeamSizes[0] = 5;
        copy.PlayerCounts[6] = new ResistanceCountRules(1, [1, 1, 1, 1, 1], [1, 1, 1, 1, 1]);

        Assert.NotSame(rules, copy);
        Assert.Equal(3, rules.PlotDeck[ResistancePlotCardType.NoConfidence]);
        Assert.Equal(2, rules.PlayerCounts[5].TeamSizes[0]);
        Assert.Equal(2, rules.PlayerCounts[6].Spies);
    }
}
