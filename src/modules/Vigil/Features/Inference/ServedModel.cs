namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// A model an inference server lists. <see cref="IsRouted"/> marks a llama-server router entry, whose metrics are
/// read per model (<c>/metrics?model=</c>); a single-model server reports no status and is always loaded.
/// </summary>
internal sealed record ServedModel(string Id, string Status, bool IsRouted, bool Failed)
{
    public const string Loaded = "loaded";
    public const string Loading = "loading";

    public bool IsLoaded => string.Equals(Status, Loaded, StringComparison.OrdinalIgnoreCase);

    public bool IsLoading => string.Equals(Status, Loading, StringComparison.OrdinalIgnoreCase);
}
