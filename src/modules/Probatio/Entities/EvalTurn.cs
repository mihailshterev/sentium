namespace Sentium.Probatio.Entities;

/// <summary>
/// One given prior message of an Eval Case; it stands in for stored conversation history.
/// </summary>
internal sealed record EvalTurn(EvalTurnRole Role, string Text);
