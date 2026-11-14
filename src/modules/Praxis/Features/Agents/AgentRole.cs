namespace Sentium.Praxis.Features.Agents;

internal static class AgentRole
{
    public const string GeneralAssistant = "GeneralAssistant";
    public const string Orchestrator = "Orchestrator";
    public const string Summarizer = "Summarizer";
    public const string Validator = "Validator";
    public const string ResearchAnalyst = "ResearchAnalyst";
    public const string SoftwareEngineer = "SoftwareEngineer";
    public const string TimelinePlanner = "TimelinePlanner";

    private static readonly HashSet<string> ControlRoles = new([Orchestrator, Validator, Summarizer], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Native roles that steer squads (plan, review, summarise) rather than doing work themselves: they are never
    /// planned as steps, offered for assignment, or chatted with.
    /// </summary>
    public static bool IsControl(string name) => ControlRoles.Contains(name);
}
