var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ApiGateway>("api-gateway");
builder.AddProject<Projects.IdentityProvider_Api>("identity-provider");

builder.Build().Run();
