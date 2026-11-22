namespace Sentium.Vigil.Features.Monitoring;

/// <summary>
/// Pushes health, incident and Device changes to the portal sessions over the realtime stream.
/// </summary>
internal interface IVigilFeed
{
    void Publish(VigilFeedEvent evt);
}
