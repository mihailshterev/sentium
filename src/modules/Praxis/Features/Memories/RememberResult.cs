namespace Sentium.Praxis.Features.Memories;

/// <param name="ReplacedExisting">
/// <see langword="true"/> when the content was close enough to an existing memory to replace it instead of adding one.
/// </param>
internal sealed record RememberResult(UserMemoryDto Memory, bool ReplacedExisting);
