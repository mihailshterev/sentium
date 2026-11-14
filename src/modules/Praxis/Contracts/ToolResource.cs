namespace Sentium.Praxis.Contracts;

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
