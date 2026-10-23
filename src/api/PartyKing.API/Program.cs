using System.Threading.RateLimiting;
using Microsoft.Extensions.FileProviders;
using PartyKing.API;
using PartyKing.API.Controllers;
using PartyKing.Application;
using PartyKing.Infrastructure;

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDefaultEndpoints();

app
    .UseHttpsRedirection()
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