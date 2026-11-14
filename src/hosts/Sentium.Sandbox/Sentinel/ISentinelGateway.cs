using Sentium.Sandbox.Models;

namespace Sentium.Sandbox.Sentinel;

/// <summary>
/// Abstracts calls to the Sentinel Policy Decision Point (hosted in Sentium.Web) so the execution
/// pipeline stays independent of the HTTP transport and can be tested without it.
/// </summary>
public interface ISentinelGateway
{
    /// <summary>
    /// Asks the Sentinel PDP whether the given agent is permitted to execute code.
    /// Implementations must be fail-closed: any PDP communication failure must return a denial.
    /// </summary>
    Task<SentinelAuthorizationResult> AuthorizeExecutionAsync(ExecutionRequest request, CancellationToken ct);
}
