using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Sentium.Registry.Contracts.Settings;
using DataAnnotations = System.ComponentModel.DataAnnotations;

namespace Sentium.Registry.Features.Settings;

internal sealed class SettingsDescriptor<T>(string key, SettingsScope scope) : ISettingsDescriptor
    where T : class, new()
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Key => key;

    public SettingsScope Scope => scope;

    public Type ValueType => typeof(T);

    public object Read(IReadOnlyDictionary<string, JsonElement> settings) =>
        settings.TryGetValue(key, out var json) ? Deserialize(json) : new T();

    public void Write(IDictionary<string, JsonElement> settings, object value) =>
        settings[key] = JsonSerializer.SerializeToElement((T)value, JsonOptions);

    public object Deserialize(JsonElement json) => json.Deserialize<T>(JsonOptions) ?? new T();

    public async Task ValidateAsync(object value, IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        var settings = (T)value;
        var failures = ValidateAnnotations(settings);

        if (serviceProvider.GetService<IValidator<T>>() is { } validator)
        {
            var result = await validator.ValidateAsync(settings, ct);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }

    private static List<ValidationFailure> ValidateAnnotations(T settings)
    {
        var results = new List<DataAnnotations.ValidationResult>();
        DataAnnotations.Validator.TryValidateObject(
            settings,
            new DataAnnotations.ValidationContext(settings),
            results,
            validateAllProperties: true);

        return
        [
            .. results.SelectMany(
                result => result.MemberNames.DefaultIfEmpty(string.Empty),
                (result, member) => new ValidationFailure(member, result.ErrorMessage))
        ];
    }
}
