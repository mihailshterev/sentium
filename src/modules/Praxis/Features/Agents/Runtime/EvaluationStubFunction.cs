using System.Text.Json;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Agents.Runtime;

/// <summary>
/// A tool with the real one's name, description and schema whose invocation only asks the Evaluation's stub for a
/// result. It holds no reference to the real tool, so nothing can reach it from here.
/// </summary>
internal sealed class EvaluationStubFunction(EvaluationToolDeclaration declaration, EvaluationToolStub stub) : AIFunction
{
    public override string Name => declaration.Name;

    public override string Description => declaration.Description;

    public override JsonElement JsonSchema => declaration.JsonSchema;

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var values = arguments.ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal);
        return await stub(declaration.Name, values, cancellationToken);
    }
}
