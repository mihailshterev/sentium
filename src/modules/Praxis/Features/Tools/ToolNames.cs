namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// The model-facing names of the Sentium tools - shared by the tools themselves and the agents' curated tool sets.
/// </summary>
public static class ToolNames
{
    public const string KnowledgeBaseSearch = "knowledge_base_search";
    public const string RecallMemory = "recall_memory";
    public const string RecallLearnings = "recall_learnings";
    public const string StoreMemory = "store_memory";
    public const string CaptureAgentLearning = "capture_agent_learning";
    public const string ExecuteCode = "execute_code_sandbox";
    public const string ScheduleRecurringTask = "schedule_recurring_task";
    public const string ReadFile = "read_file";
    public const string ListWorkspaces = "list_workspaces";
    public const string ListWorkspaceFiles = "list_workspace_files";
    public const string ReadWorkspaceFileContent = "read_file_content";
    public const string WriteWorkspaceFile = "write_workspace_file";
    public const string FindAssets = "find_assets";
    public const string GetAsset = "get_asset";
    public const string ListLocationContents = "list_location_contents";
}
