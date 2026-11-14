namespace Sentium.Praxis.Features.Tools.Timeline;

internal static class TimelineToolMessages
{
    public const string NoUser = "Error: The timeline belongs to a signed-in user, and no user is associated with this run.";
    public const string Unavailable = "Error: The timeline could not be read right now.";
    public const string WriteFailed = "Error: The timeline could not be updated right now.";

    public static DateOnly Today(TimeProvider timeProvider) => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
