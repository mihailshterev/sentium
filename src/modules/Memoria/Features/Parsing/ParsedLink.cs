namespace Sentium.Memoria.Features.Parsing;

/// <param name="Target">The target as written, without heading or alias.</param>
/// <param name="TargetKey">The normalized key the resolver matches (lower-case, no <c>.md</c>).</param>
internal sealed record ParsedLink(string Target, string TargetKey, string? Heading, bool IsEmbed);
