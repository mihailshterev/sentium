using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentium.Identity.Contracts;
using Sentium.Infrastructure.Extensions;
using Sentium.Probatio.Data;
using Sentium.Probatio.Features.Benchmarks;
using Sentium.Probatio.Features.Evaluations;
using Sentium.Probatio.Features.Suites;

namespace Sentium.Probatio;

/// <summary>
/// Probatio: measures how well an Agent does on a model. Users author private, versioned Eval Suites of Eval Cases
/// and their Checks, and run Evaluations of an agent on a model, or Benchmarks across several models, against them in the background, with tools never
/// executed (ADR 0003).
/// </summary>
public static class ProbatioModuleExtensions
{
    internal const string ModuleName = "probatio";

    public static IHostApplicationBuilder AddProbatioModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<ProbatioDbContext>(ProbatioDbContext.Schema);

        var services = builder.Services;

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IEvalSuiteService, EvalSuiteService>();

        services.AddSingleton<EvaluationQueue>();
        services.AddSingleton<EvaluationCancellations>();
        services.AddScoped<EvaluationLauncher>();
        services.AddScoped<EvaluationExecutor>();
        services.AddScoped<IEvaluationService, EvaluationService>();
        services.AddScoped<IBenchmarkService, BenchmarkService>();
        services.AddHostedService<EvaluationWorker>();
        services.AddSingleton<IUserRemovalObserver, ProbatioUserDataRemover>();

        services.AddHealthChecks().AddDbContextCheck<ProbatioDbContext>("probatio-db", tags: [ModuleName]);

        return builder;
    }

    public static async Task InitializeProbatioModuleAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await app.Services.MigrateModuleDbContextAsync<ProbatioDbContext>();
    }
}
