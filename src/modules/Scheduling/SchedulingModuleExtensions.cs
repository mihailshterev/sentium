using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;
using Sentium.Core.Constants;
using Sentium.Identity.Contracts;
using Sentium.Infrastructure.BackgroundWork;
using Sentium.Praxis.Contracts;
using Sentium.Scheduling.Features.BackgroundJobs;
using Sentium.Scheduling.Features.Jobs;

namespace Sentium.Scheduling;

/// <summary>
/// Recurring work: a persistent Quartz scheduler (tables in the <c>praxis</c> schema, created by the Praxis
/// <c>AddQuartzJobStore</c> migration) running agent-created scripts and user-created agent and workflow Schedules, the
/// approval-gated <c>schedule_recurring_task</c> agent tool, the scheduler endpoints and the background-jobs overview.
/// The module has no DbContext of its own, so it has nothing to migrate.
/// </summary>
public static class SchedulingModuleExtensions
{
    public static IHostApplicationBuilder AddSchedulingModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

        services.AddTransient<IAgentTool, ScheduleTaskTool>();
        services.AddSingleton<IUserRemovalObserver, ScheduledJobRemover>();
        services.AddSingleton<IUnattendedRunObserver, ScheduleRunRecorder>();
        services.AddSingleton<OwnerTimeZoneResolver>();
        services.AddScoped<ScheduleService>();
        services.AddSingleton<IBackgroundActivitySource, ExecutingScheduleSource>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddQuartz(q =>
        {
            q.SchedulerName = SchedulerDefaults.SchedulerName;
            q.SchedulerId = "AUTO";

            q.MisfireThreshold = TimeSpan.FromMinutes(1);

            q.AddJobListener<ScheduleChangeListener>();

            q.UsePersistentStore(store =>
            {
                store.UseProperties = true;
                store.PerformSchemaValidation = true;
                store.RetryInterval = TimeSpan.FromSeconds(15);

                store.UsePostgres(postgres =>
                {
                    postgres.ConnectionStringName = ResourceNames.SentiumDb;
                    postgres.TablePrefix = SchedulerDefaults.TablePrefix;
                });

                store.UseSystemTextJsonSerializer();
            });
        });

        services.AddQuartzHostedService(options =>
        {
            options.AwaitApplicationStarted = true;
            options.WaitForJobsToComplete = true;
        });

        return builder;
    }
}
