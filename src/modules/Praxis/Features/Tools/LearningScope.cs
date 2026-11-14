namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Visibility requested for a captured learning.
/// </summary>
public enum LearningScope
{
    /// <summary>
    /// Private to the acting user (default).
    /// </summary>
    User,

    /// <summary>
    /// Shared with every user's agents - only granted after the sanitization pipeline approves it.
    /// </summary>
    Global
}
