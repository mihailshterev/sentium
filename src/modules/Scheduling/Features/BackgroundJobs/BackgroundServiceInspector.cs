using System.Text;

namespace Sentium.Scheduling.Features.BackgroundJobs;

/// <summary>
/// Describes the registered hosted services from what the host already knows: a <see cref="BackgroundService"/>'s
/// <see cref="BackgroundService.ExecuteTask"/> tells whether its loop is running, stopped or faulted. Framework services
/// (<c>Microsoft.*</c>) are left out.
/// </summary>
internal static class BackgroundServiceInspector
{
    private const string FrameworkNamespace = "Microsoft.";
    private const string SentiumNamespace = "Sentium.";

    public static IReadOnlyList<BackgroundServiceResponse> Describe(IEnumerable<IHostedService> services)
    {
        return
        [
            .. services
                .Where(service => service.GetType().Namespace?.StartsWith(FrameworkNamespace, StringComparison.Ordinal) != true)
                .Select(Describe)
                .OrderBy(s => s.Module, StringComparer.Ordinal)
                .ThenBy(s => s.Name, StringComparer.Ordinal)
        ];
    }

    private static BackgroundServiceResponse Describe(IHostedService service)
    {
        var type = service.GetType();
        var (state, error) = service is BackgroundService background ? StateOf(background.ExecuteTask) : ("Hosted", null);

        return new BackgroundServiceResponse(Humanize(type.Name), type.FullName ?? type.Name, ModuleOf(type.Namespace), state, error);
    }

    private static (string State, string? Error) StateOf(Task? execution)
    {
        if (execution is null)
        {
            return ("NotStarted", null);
        }

        if (execution.IsFaulted)
        {
            return ("Faulted", execution.Exception?.GetBaseException().Message);
        }

        return execution.IsCompleted ? ("Stopped", null) : ("Running", null);
    }

    /// <summary>
    /// <c>Sentium.Praxis.Features...</c> is Praxis; a third-party namespace is named by its root (<c>Quartz</c>).
    /// </summary>
    private static string ModuleOf(string? ns)
    {
        if (string.IsNullOrEmpty(ns))
        {
            return "Other";
        }

        var segments = ns.Split('.');
        return ns.StartsWith(SentiumNamespace, StringComparison.Ordinal) && segments.Length > 1 ? segments[1] : segments[0];
    }

    /// <summary>
    /// <c>WorkflowRunProcessor</c> becomes "Workflow run processor"; a generic arity suffix is dropped.
    /// </summary>
    private static string Humanize(string typeName)
    {
        var name = typeName.Split('`')[0];
        var text = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            var startsWord = i > 0 && char.IsUpper(c) && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1])));

            if (startsWord)
            {
                text.Append(' ').Append(char.ToLowerInvariant(c));
            }
            else
            {
                text.Append(c);
            }
        }

        return text.ToString();
    }
}
