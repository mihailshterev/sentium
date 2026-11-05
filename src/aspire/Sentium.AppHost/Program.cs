// Sentium runs as a modular monolith (Sentium.Web) next to the separate, stateless Sentium.Sandbox.
// Pass --TestRun=true for the e2e variant: Testing environment, throwaway data and no model pulls.
var builder = DistributedApplication.CreateBuilder(args);

MonolithConfiguration.Configure(builder, testRun: args.Contains("--TestRun=true"));

builder.Build().Run();
