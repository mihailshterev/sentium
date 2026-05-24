using Sentium.Watchdog.Application;
using Sentium.Shared.Constants;
using Sentium.Infrastructure.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAuthenticationDefaults();

builder.AddNatsClient(ResourceNames.Nats);
builder.AddRedisDistributedCache(ResourceNames.Redis);

builder.Services.AddOpenApi();
builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.AddSentiumAuditLogging();
builder.Services.AddWatchdogApplication();

var app = builder.Build();

app.UseSentiumTracing();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Sentium Watchdog")
               .WithTheme(ScalarTheme.DeepSpace)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

namespace Sentium.Watchdog.Api
{
    public partial class Program { }
}
