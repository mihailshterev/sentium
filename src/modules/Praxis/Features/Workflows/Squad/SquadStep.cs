using System.ComponentModel;

namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// One planned step: which agent does what, and what a correct result contains.
/// </summary>
internal sealed record SquadStep(
    [property: Description("The agent that performs this step, exactly as named in the roster.")] string Agent,
    [property: Description("One concrete instruction telling the agent what to produce.")] string Task,
    [property: Description("What a correct result for this step contains.")] string ExpectedOutput);
