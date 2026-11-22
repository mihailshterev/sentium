using System.ComponentModel.DataAnnotations;

namespace Sentium.Praxis.Features.Assistant;

/// <summary>
/// The <c>Assistant</c> configuration section.
/// </summary>
internal sealed class AssistantOptions
{
    public const string SectionName = "Assistant";

    public TimeSpan TurnTimeout { get; set; } = TimeSpan.FromMinutes(30);

    [Range(1, 64)]
    public int MaxConcurrentTurns { get; set; } = 8;
}
