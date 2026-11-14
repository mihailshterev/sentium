namespace Sentium.Scheduling;

/// <summary>
/// HTTP route prefix for the scheduler endpoints hosted by Sentium.Web. Scheduling was split out of Praxis; the
/// prefix stays <c>api/praxis</c> so the portal's URLs are unchanged.
/// </summary>
internal static class SchedulingRoutes
{
    public const string Prefix = "api/praxis";
}
