namespace Sentium.Praxis.Contracts;

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
