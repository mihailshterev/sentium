var builder = DistributedApplication.CreateBuilder(args);

var identityApi = builder.AddProject<Projects.IdentityProvider_Api>("identity-provider");
var sentinelApi = builder.AddProject<Projects.Sentinel_Api>("sentinel");

builder.AddProject<Projects.ApiGateway>("gateway")
    .WithReference(identityApi)
    .WithReference(sentinelApi);

builder.Build().Run();
