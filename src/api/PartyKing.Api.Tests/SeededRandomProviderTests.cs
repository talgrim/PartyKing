using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PartyKing.API.Development;
using PartyKing.Application.System;

namespace PartyKing.Api.Tests;

public class SeededRandomProviderTests
{
    private static IRandomProvider Resolve(string environment, string? seed)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRandomProvider, TestDefaultRandomProvider>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(seed is null ? [] : new Dictionary<string, string?> { ["Resistance:Seed"] = seed })
            .Build();
        SeededRandomProvider.AddIfConfigured(services, new FakeEnvironment(environment), configuration);

        return services.BuildServiceProvider().GetRequiredService<IRandomProvider>();
    }

    [Fact]
    public void Create_ReturnsTheSameSequenceEveryTime()
    {
        var provider = new SeededRandomProvider(42);

        var first = Enumerable.Range(0, 5).Select(_ => provider.Create().Next()).ToList();
        var second = Enumerable.Range(0, 5).Select(_ => provider.Create().Next()).ToList();

        Assert.Equal(Enumerable.Repeat(new Random(42).Next(), 5), first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void AddIfConfigured_InDevelopmentWithASeed_UsesTheSeededProvider()
    {
        Assert.IsType<SeededRandomProvider>(Resolve(Environments.Development, "7"));
    }

    [Fact]
    public void AddIfConfigured_InDevelopmentWithoutASeed_KeepsTheDefault()
    {
        Assert.IsType<TestDefaultRandomProvider>(Resolve(Environments.Development, null));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void AddIfConfigured_OutsideDevelopment_IgnoresTheSeed(string environment)
    {
        Assert.IsType<TestDefaultRandomProvider>(Resolve(environment, "7"));
    }

    private sealed class TestDefaultRandomProvider : IRandomProvider
    {
        public Random Create() => Random.Shared;
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
