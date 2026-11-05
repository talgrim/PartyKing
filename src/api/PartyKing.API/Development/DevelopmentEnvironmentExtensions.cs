namespace PartyKing.API.Development;

public static class DevelopmentEnvironmentExtensions
{
    /// <summary>The environment the e2e suite runs the API in, so it can have its own <c>appsettings.e2e.json</c>.</summary>
    public const string E2eEnvironment = "e2e";

    /// <summary>
    /// True in Development and in the e2e environment. The development helpers (CORS for the dev UI, Swagger, the dev
    /// personas, the switches that lift the rate limits or seed the random source) are tied to this, never to a setting
    /// alone, so a deployed instance (Production) cannot be given them by accident.
    /// </summary>
    public static bool IsDevelopmentOrE2e(this IHostEnvironment environment)
    {
        return environment.IsDevelopment() || environment.IsEnvironment(E2eEnvironment);
    }
}
