using PartyKing.API;
using PartyKing.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.RegisterServices();

builder.Services.AddCors(options => options.AddPolicy("CorsPolicy", policy =>
{
    policy
        .AllowAnyHeader()
        .AllowAnyMethod()
        .SetIsOriginAllowed(_ => true)
        .AllowCredentials();
}));

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
    .UseExceptionHandler("/error");

app.MapControllers();

await app.Services.MigrateDb();

app.Run();