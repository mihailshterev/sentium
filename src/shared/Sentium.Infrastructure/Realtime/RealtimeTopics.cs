namespace Sentium.Infrastructure.Realtime;

/// <summary>
/// The realtime stream's topics. The portal mirrors this list in <c>shared/realtime/topics.ts</c>; renaming one is a
/// breaking change for open sessions.
/// </summary>
public static class RealtimeTopics
{
    /// <summary>
    /// The first frame of every connection: the portal refetches everything it shows, covering events missed while
    /// disconnected.
    /// </summary>
    public const string Connected = "connected";

    /// <summary>
    /// A Notification was added, updated or read; carries <c>{ type }</c>, and the Notification for a new one.
    /// </summary>
    public const string Notifications = "notifications";

    public const string VigilStatus = "vigil.status";
    public const string VigilIncidentOpened = "vigil.incident.opened";
    public const string VigilIncidentResolved = "vigil.incident.resolved";
    public const string VigilDeviceUpdated = "vigil.device.updated";
    public const string VigilDeviceRemoved = "vigil.device.removed";

    public const string WorkspaceFiles = "workspaces.files";
    public const string WorkflowRuns = "workflows.runs";
    public const string SandboxExecutions = "sandbox.executions";
    public const string InventoryKnowledge = "inventory.knowledge";
    public const string Evaluations = "evaluations";
    public const string Schedules = "scheduler.jobs";
    public const string SentinelAudit = "sentinel.audit";
    public const string Vault = "vault";
}
