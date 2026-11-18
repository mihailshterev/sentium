using System.Text.Json;

namespace Sentium.Praxis.Contracts;

/// <summary>
/// A tool as the model under evaluation sees it: name, description and parameter schema, without anything that could
/// run it.
/// </summary>
public sealed record EvaluationToolDeclaration(string Name, string Description, JsonElement JsonSchema);
