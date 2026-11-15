namespace Sentium.Vigil.Features.Devices;

/// <param name="NextReportSeconds">
/// How long the Device waits before its next Report, so the cadence is changed on the server without shipping a new
/// app.
/// </param>
internal sealed record DeviceReportResponse(int NextReportSeconds);
