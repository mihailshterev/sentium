using System.ComponentModel;

namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// The ordered plan a squad executes.
/// </summary>
internal sealed record SquadPlan([property: Description("The steps, in execution order.")] IReadOnlyList<SquadStep> Steps);
