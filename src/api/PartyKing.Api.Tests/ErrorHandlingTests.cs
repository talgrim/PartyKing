using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PartyKing.API;

namespace PartyKing.Api.Tests;

public class ErrorHandlingTests
{
    private static async Task<HttpResponseMessage> GetFromAppThatThrowsAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddErrorHandling();

        await using var app = builder.Build();
        app.UseErrorHandling();
        app.Run(_ => throw new InvalidOperationException("boom"));

        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            using var client = new HttpClient();

            return await client.GetAsync(address);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task UseErrorHandling_WhenARequestThrows_AnswersWithAServerError()
    {
        using var response = await GetFromAppThatThrowsAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task UseErrorHandling_WhenARequestThrows_AnswersWithProblemDetails()
    {
        using var response = await GetFromAppThatThrowsAsync();

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UseErrorHandling_WhenARequestThrows_DoesNotLeakTheExceptionMessage()
    {
        using var response = await GetFromAppThatThrowsAsync();

        Assert.DoesNotContain("boom", await response.Content.ReadAsStringAsync());
    }
}
