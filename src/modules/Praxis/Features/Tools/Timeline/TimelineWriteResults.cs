using Sentium.Core.Results;

namespace Sentium.Praxis.Features.Tools.Timeline;

internal static class TimelineWriteResults
{
    public static string Describe<T>(Result<T> result)
    {
        return result.Status switch
        {
            ResultStatus.NotFound => $"Error: {result.Error ?? "Not found."} Use {ToolNames.ListTimelineTasks} to find task ids.",
            ResultStatus.Conflict => $"Error: {result.Error}",
            _ => $"Error: {result.Error ?? "The request was invalid."}"
        };
    }
}
