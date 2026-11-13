using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using PartyKing.API;
using PartyKing.API.Controllers;
using PartyKing.API.Controllers.Games;
using PartyKing.API.Development;
using PartyKing.API.Hubs;
using PartyKing.Application;
using PartyKing.Application.Configuration;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Infrastructure;

// `dotnet run --project PartyKing.API -- hash-password [password]` prints a hash for AdminSettings:PasswordHash
if (args is ["hash-password", ..])
{
    return PasswordHashTool.Run(args.Skip(1).ToArray());
}

// The web root is no longer in git and is only picked up by the host when it exists, so create it first
Directory.CreateDirectory(
    Environment.GetEnvironmentVariable("ASPNETCORE_WEBROOT") ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddErrorHandling();
builder.Services.RegisterServices(builder.Configuration);
SeededRandomProvider.AddIfConfigured(builder.Services, builder.Environment, builder.Configuration);

// The dev UI (port 2137) calls the API (port 4200) cross-origin; in production both share one origin behind nginx
const string corsPolicy = "CorsPolicy";
if (builder.Environment.IsDevelopmentOrE2e())
{
    builder.Services.AddCors(options => options.AddPolicy(corsPolicy, policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true)
            .AllowCredentials()
            // Without it Firefox repeats the preflight before almost every cross-origin JSON POST
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    }));
}

var demoSettings = builder.Configuration.GetSection(DemoConfiguration.SectionName).Get<DemoConfiguration>()
    ?? new DemoConfiguration();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Several guests can share one Wi-Fi (one IP), so signing in is more generous than the session password.
    // Changing and deleting an account check a password too, so they share this limit
    options.AddPolicy(AccountController.LoginRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1)
            }));
    // Every registration costs a PBKDF2 run and a row
    options.AddPolicy(AccountController.RegisterRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
    // Every attempt costs a PBKDF2 run, and a short password is easy to guess, so guessing is capped per IP
    options.AddPolicy(SessionsController.JoinRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
    // Every start makes a session with a host and its files, and a demo is open to anybody
    options.AddPolicy(DemoController.StartRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = demoSettings.StartsPerHourPerIp,
                Window = TimeSpan.FromHours(1)
            }));
    // Every request spends the free quota of the language model
    options.AddPolicy(GameControllerBase.SuggestionsRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
    // Every request writes a zip of all the session's photos to disk
    options.AddPolicy(SlideshowController.ArchiveRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            }));
    // Several guests can share one Wi-Fi (one IP), so the limit is generous
    options.AddPolicy(SlideshowController.UploadRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1)
            }));
});

builder.Services.AddControllers();

// Behind nginx/Fly the real client IP and scheme arrive in X-Forwarded-* headers. Kestrel is only reachable
// through the proxy in the container, so every proxy is trusted.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopmentOrE2e())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();

app.MapDefaultEndpoints();

// TLS is terminated by the proxy in front of the container
if (app.Environment.IsDevelopmentOrE2e())
{
    app.UseHttpsRedirection();
}

app.UseRouting();

if (app.Environment.IsDevelopmentOrE2e())
{
    app.UseCors(corsPolicy);
    app.UseMiddleware<DevPersonaMiddleware>();
}

// The e2e suite logs in and unlocks sessions from one IP far more often than the limits allow. Without the
// middleware the [EnableRateLimiting] attributes do nothing. Honoured in Development and e2e only, so a stray
// setting can never lift the limits of a deployed instance.
if (!(app.Environment.IsDevelopmentOrE2e() && app.Configuration.GetValue<bool>("RateLimiting:Disabled")))
{
    app.UseRateLimiter();
}

app
    .UseAuthentication()
    .UseAuthorization()
    .UseErrorHandling()
    .UseStaticFiles(CreateImagesOnlyStaticFiles());

app.MapControllers();
app.MapHub<SessionHub>(SessionHub.Path);

await app.Services.MigrateDb();
await app.Services.SeedAdminAsync();
await app.Services.GetRequiredService<ISlideshowService>().InitializeAsync(app.Environment.WebRootPath, CancellationToken.None);

app.Run();

return 0;

// Uploads are served from the web root, so only image types may be served and a browser must never sniff
// an upload into something executable (an uploaded .html or .svg would run on the app's origin).
static StaticFileOptions CreateImagesOnlyStaticFiles()
{
    var contentTypes = new FileExtensionContentTypeProvider();
    contentTypes.Mappings.Clear();
    contentTypes.Mappings[".jpg"] = "image/jpeg";
    contentTypes.Mappings[".jpeg"] = "image/jpeg";
    contentTypes.Mappings[".png"] = "image/png";
    contentTypes.Mappings[".gif"] = "image/gif";
    contentTypes.Mappings[".webp"] = "image/webp";

    return new StaticFileOptions
    {
        ContentTypeProvider = contentTypes,
        ServeUnknownFileTypes = false,
        OnPrepareResponse = context =>
            context.Context.Response.Headers.XContentTypeOptions = "nosniff"
    };
}
