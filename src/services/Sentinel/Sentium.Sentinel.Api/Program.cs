using Scalar.AspNetCore;
using Sentium.Infrastructure.Extensions;
using Sentium.Sentinel.Application;
using Sentium.Sentinel.Infrastructure;
using Sentium.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAuthenticationDefaults();

builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.AddNatsClient(ResourceNames.Nats);

builder.AddSentiumAuditLogging();
builder.AddInfrastructure();
builder.Services.AddApplication(builder.Configuration);

var app = builder.Build();

app.UseSentiumTracing();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Sentium Sentinel")
                .WithTheme(ScalarTheme.DeepSpace)
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
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

