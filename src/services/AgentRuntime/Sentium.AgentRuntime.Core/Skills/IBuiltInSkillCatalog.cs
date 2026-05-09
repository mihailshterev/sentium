namespace Sentium.AgentRuntime.Core.Skills;

/// <summary>
/// Provides metadata for all class-based (built-in) skills registered in the system.
/// </summary>
public interface IBuiltInSkillCatalog
{
    /// <summary>
    /// Returns the full list of built-in skill descriptors.
    /// </summary>
    IReadOnlyList<BuiltInSkillInfo> GetAll();
}
