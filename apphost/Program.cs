var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ApiGateway>("api-gateway");

builder.Build().Run();
