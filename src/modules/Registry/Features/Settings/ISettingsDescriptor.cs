using System.Text.Json;
using Sentium.Registry.Contracts.Settings;

namespace Sentium.Registry.Features.Settings;

/// <summary>
/// Describes a single typed settings key: its scope and how to read, write, deserialize, and validate its value.
/// A settings row stores every key's value as JSON under the key itself.
/// </summary>
internal interface ISettingsDescriptor
{
    string Key { get; }
    SettingsScope Scope { get; }

    /// <summary>
    /// The settings contract this key stores.
    /// </summary>
    Type ValueType { get; }

    /// <summary>
    /// Reads this key's value out of a settings row, or the contract's defaults when the row has none.
    /// </summary>
    object Read(IReadOnlyDictionary<string, JsonElement> settings);

    void Write(IDictionary<string, JsonElement> settings, object value);

    /// <summary>
    /// Deserializes raw JSON into the strongly-typed value for this key.
    /// </summary>
    object Deserialize(JsonElement json);

    /// <summary>
    /// Validates a value before it is persisted against the contract's DataAnnotations and its optional
    /// FluentValidation validator, throwing a FluentValidation <c>ValidationException</c> with every failure.
    /// </summary>
    Task ValidateAsync(object value, IServiceProvider serviceProvider, CancellationToken ct = default);
}
