var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.PartyKing_API>("partyking-api");

builder.AddNpmApp("ui", "../PartyKing.UI")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port:2137, targetPort: 2137, isProxied: false)
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();