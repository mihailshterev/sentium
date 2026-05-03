using IdentityProvider.Application;
using IdentityProvider.Infrastructure;
using Sentium.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.AddNatsClient(ResourceNames.Nats);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentityServices(builder.Configuration);
builder.Services.AddApplication();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Applying database migrations...");
    await app.ApplyMigrations();
    logger.LogInformation("Database migrations applied");

    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
