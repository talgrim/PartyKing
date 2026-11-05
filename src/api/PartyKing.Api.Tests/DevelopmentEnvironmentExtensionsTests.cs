using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PartyKing.API.Development;

namespace PartyKing.Api.Tests;

public class DevelopmentEnvironmentExtensionsTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("e2e", true)]
    [InlineData("E2E", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void IsDevelopmentOrE2e_OnlyForDevelopmentAndE2e(string environment, bool expected)
    {
        Assert.Equal(expected, new FakeEnvironment(environment).IsDevelopmentOrE2e());
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
