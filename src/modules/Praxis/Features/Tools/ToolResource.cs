namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Resource class a tool touches, as understood by the Sentinel policy engine (names match Sentinel's resource types).
/// </summary>
public enum ToolResource
{
    File,
    VectorDb,
    Memory,
    Code,
    External,
    Inventory,
    Timeline,
    Any
}

/// <summary>
/// Kind of access a tool performs, as understood by the Sentinel policy engine.
/// </summary>
public enum ToolAction
{
    Read,
    Search,
    Write,
    Execute
}
