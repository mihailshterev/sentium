namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// The model-facing names of the Sentium tools - shared by the tools themselves and the agents' curated tool sets.
/// </summary>
internal static class ToolNames
{
    public const string KnowledgeBaseSearch = "knowledge_base_search";
    public const string RecallMemory = "recall_memory";
    public const string RecallLearnings = "recall_learnings";
    public const string StoreMemory = "store_memory";
    public const string CaptureAgentLearning = "capture_agent_learning";
    public const string ExecuteCode = "execute_code_sandbox";
    public const string ReadFile = "read_file";
    public const string ListWorkspaces = "list_workspaces";
    public const string ListWorkspaceFiles = "list_workspace_files";
    public const string ReadWorkspaceFileContent = "read_file_content";
    public const string WriteWorkspaceFile = "write_workspace_file";
    public const string FindAssets = "find_assets";
    public const string GetAsset = "get_asset";
    public const string ListLocationContents = "list_location_contents";
    public const string GetTimelineOverview = "get_timeline_overview";
    public const string ListTimelineTasks = "list_timeline_tasks";
    public const string GetTimelineTask = "get_timeline_task";
    public const string ProposeTimelineTask = "propose_timeline_task";
    public const string ProposeTaskReschedule = "propose_task_reschedule";
    public const string AddTimelineNote = "add_timeline_note";
}
