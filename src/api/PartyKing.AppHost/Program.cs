var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.PartyKing_API>("partyking-api");

builder.AddNpmApp("ui", "../../ui/PartyKing.UI")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port:2137, targetPort: 2137, isProxied: false)
    .WithExternalHttpEndpoints()
    // The dashboard link must use 127.0.0.1: the API's login cookies don't reach a page opened via localhost
    .WithUrlForEndpoint("http", url => url.Url = url.Url.Replace("localhost", "127.0.0.1"))
    .PublishAsDockerFile();

builder.Build().Run();