namespace Sentium.Praxis.Features.Agents.Runtime;

/// <summary>
/// Names of the native (built-in) agents. They are reserved: users cannot create database agents with these names.
/// </summary>
public interface IAgentRegistry
{
    IEnumerable<string> GetRegisteredNames();
}
