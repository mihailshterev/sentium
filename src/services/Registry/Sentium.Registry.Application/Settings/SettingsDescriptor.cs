using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Registry.Core.Settings;

namespace Sentium.Registry.Application.Settings;

public sealed class SettingsDescriptor<T>(
    string key,
    SettingsScope scope,
    Func<SettingsContainer, T> read,
    Action<SettingsContainer, T> write) : ISettingsDescriptor
    where T : class, new()
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Key => key;

    public SettingsScope Scope => scope;

    public object Read(SettingsContainer container) => read(container) ?? new T();

    public void Write(SettingsContainer container, object value) => write(container, (T)value);

    public object Deserialize(JsonElement json) => json.Deserialize<T>(JsonOptions) ?? new T();

    public async Task ValidateAsync(object value, IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        if (serviceProvider.GetService<IValidator<T>>() is { } validator)
        {
            var result = await validator.ValidateAsync((T)value, ct);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }
    }
}
