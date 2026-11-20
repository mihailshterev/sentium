namespace Sentium.Praxis.Contracts;

public enum UnattendedRunLaunch
{
    Queued,

    /// <summary>
    /// The previous run of the same Schedule is still queued or running, so this fire was dropped.
    /// </summary>
    SkippedStillRunning
}
