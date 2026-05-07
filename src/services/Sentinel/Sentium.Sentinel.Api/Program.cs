using Sentium.Infrastructure.Extensions;
using Sentium.Sentinel.Application;
using Sentium.Sentinel.Infrastructure;
using Sentium.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAuthenticationDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.AddNatsClient(ResourceNames.Nats);

builder.AddSentiumAuditLogging();
builder.Services.AddInfrastructure();
builder.Services.AddApplication();

var app = builder.Build();

app.UseSentiumTracing();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();


namespace Sentium.Sentinel.Api
{
    public partial class Program { }
}
