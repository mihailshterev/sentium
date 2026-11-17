namespace Sentium.Infrastructure.Storage;

/// <summary>
/// An object opened by <see cref="IObjectStore.OpenReadAsync"/>. Disposing <see cref="Content"/> releases the
/// connection to the store.
/// </summary>
public sealed record StoredObject(
    Stream Content,
    string ContentType,
    long Length,
    IReadOnlyDictionary<string, string> Metadata);
