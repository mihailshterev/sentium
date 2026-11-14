namespace Sentium.Praxis.Features.Models;

internal enum ModelActionResult
{
    /// <summary>
    /// The server accepted the request.
    /// </summary>
    Accepted,

    /// <summary>
    /// The model is not declared on the server.
    /// </summary>
    NotFound,

    /// <summary>
    /// The model is in a state that does not allow the action (e.g. load while it is still running).
    /// </summary>
    Conflict,

    /// <summary>
    /// The server is unreachable or failed.
    /// </summary>
    Failed
}
