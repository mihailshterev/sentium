namespace Sentium.Vigil.Features.Monitoring;

/// <summary>
/// A probe whose incidents notify under their own Nuntius category instead of <c>system.incident</c>, so
/// recipients can tell them apart.
/// </summary>
internal interface IIncidentCategoryProbe : IHealthProbe
{
    string NotificationCategory { get; }
}
