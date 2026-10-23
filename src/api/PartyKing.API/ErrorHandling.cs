namespace PartyKing.API;

public static class ErrorHandling
{
    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        return services.AddProblemDetails();
    }

    // No path: a handler that re-executes a path with no endpoint behind it answers 404 and the middleware then throws
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder app)
    {
        return app.UseExceptionHandler();
    }
}
