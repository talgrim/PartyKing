using System.Reflection;

namespace PartyKing.Architecture.Tests;

/// <summary>
/// Pins the layering described in CLAUDE.md. A compiled assembly only references the assemblies whose types it
/// really uses, so these checks fail on a using that matters, not on an unused package reference.
/// </summary>
public class LayerDependencyTests
{
    private static readonly HashSet<string> InfrastructureAssemblies =
    [
        "PartyKing.Infrastructure",
        "PartyKing.Infrastructure.Llm",
        "PartyKing.Infrastructure.Lyrics",
        "PartyKing.Infrastructure.Spotify",
        "PartyKing.Infrastructure.Youtube"
    ];

    // HttpContext, IFormFile, controllers and routing: the web layer. DataProtection is not in this list on purpose
    // (JoinTokenProtector signs the QR tokens with it), it knows nothing about HTTP.
    private static readonly string[] WebPrefixes =
    [
        "Microsoft.AspNetCore.Http",
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.AspNetCore.Routing",
        "Microsoft.AspNetCore.Authentication",
        "Microsoft.AspNetCore.SignalR"
    ];

    [Fact]
    public void Domain_ReferencesNoPartyKingOrMicrosoftAssembly()
    {
        var references = ReferencesOf("PartyKing.Domain");

        Assert.DoesNotContain(references, x => x.StartsWith("PartyKing") || x.StartsWith("Microsoft"));
    }

    [Fact]
    public void Domain_DoesNotKnowHowItIsSerialized()
    {
        var references = ReferencesOf("PartyKing.Domain");

        Assert.DoesNotContain("System.Text.Json", references);
    }

    [Fact]
    public void Contract_ReferencesNoPartyKingAssembly()
    {
        var references = ReferencesOf("PartyKing.Contract");

        Assert.DoesNotContain(references, x => x.StartsWith("PartyKing"));
    }

    [Fact]
    public void Application_ReferencesOnlyDomainAndContract()
    {
        var references = ReferencesOf("PartyKing.Application").Where(x => x.StartsWith("PartyKing"));

        Assert.DoesNotContain(references, x => x is not ("PartyKing.Contract" or "PartyKing.Domain"));
    }

    [Fact]
    public void Application_SeesNoHttp()
    {
        var references = ReferencesOf("PartyKing.Application");

        Assert.DoesNotContain(references, IsWebAssembly);
    }

    [Fact]
    public void TheHttpCheck_RecognizesTheWebLayer()
    {
        var references = ReferencesOf("PartyKing.API");

        Assert.Contains(references, IsWebAssembly);
    }

    [Fact]
    public void Persistence_ReferencesOnlyDomain()
    {
        var references = ReferencesOf("PartyKing.Persistence").Where(x => x.StartsWith("PartyKing"));

        Assert.Equal(["PartyKing.Domain"], references.ToArray());
    }

    [Theory]
    [MemberData(nameof(Infrastructure))]
    public void Infrastructure_SeesNoHttp(string assembly)
    {
        var references = ReferencesOf(assembly);

        Assert.DoesNotContain(references, IsWebAssembly);
    }

    [Theory]
    [MemberData(nameof(Infrastructure))]
    public void Infrastructure_DoesNotReferenceTheApiOrAnotherInfrastructureProject(string assembly)
    {
        var references = ReferencesOf(assembly).Where(x => x.StartsWith("PartyKing"));

        var forbidden = references
            .Where(x => x == "PartyKing.API" || (InfrastructureAssemblies.Contains(x) && x != assembly));

        Assert.Empty(forbidden);
    }

    [Theory]
    [MemberData(nameof(Infrastructure))]
    public void OnlyTheMainInfrastructureProject_UsesPersistence(string assembly)
    {
        var references = ReferencesOf(assembly);

        Assert.Equal(assembly == "PartyKing.Infrastructure", references.Contains("PartyKing.Persistence"));
    }

    public static TheoryData<string> Infrastructure()
    {
        var data = new TheoryData<string>();
        foreach (var assembly in InfrastructureAssemblies)
        {
            data.Add(assembly);
        }

        return data;
    }

    private static bool IsWebAssembly(string name)
    {
        return WebPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static HashSet<string> ReferencesOf(string assemblyName)
    {
        return Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(x => x.Name!)
            .ToHashSet();
    }
}
