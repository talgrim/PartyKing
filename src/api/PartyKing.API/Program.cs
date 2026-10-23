using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using PartyKing.API;
using PartyKing.API.Controllers;
using PartyKing.Application;
using PartyKing.Infrastructure;

// `dotnet run --project PartyKing.API -- hash-password [password]` prints a hash for AdminSettings:PasswordHash
if (args is ["hash-password", ..])
{
    return PasswordHashTool.Run(args.Skip(1).ToArray());
}

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.RegisterServices(builder.Configuration);

builder.Services.AddCors(options => options.AddPolicy("CorsPolicy", policy =>
{
    policy
        .AllowAnyHeader()
        .AllowAnyMethod()
        .SetIsOriginAllowed(_ => true)
        .AllowCredentials();
}));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AdminController.LoginRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
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

// Photo uploads can be several files in one request
const long maxUploadBytes = 200L * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxUploadBytes);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxUploadBytes);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();

app.MapDefaultEndpoints();

// TLS is terminated by the proxy in front of the container
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app
    .UseRouting()
    .UseCors("CorsPolicy")
    .UseRateLimiter()
    .UseAuthentication()
    .UseAuthorization()
    .UseExceptionHandler("/error")
    .UseStaticFiles();

app.MapControllers();

await app.Services.MigrateDb();
await app.Services.SeedAdminAsync();

app.Run();

return 0;
