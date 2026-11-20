using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Registry.Contracts.Settings;
using Sentium.Registry.Features.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Registry;

/// <summary>
/// Validates settings the way a save does: the contract's DataAnnotations plus its FluentValidation validator.
/// </summary>
internal static class SettingsValidation
{
    public static async Task<bool> IsValidAsync<T>(T settings, IValidator<T>? validator = null)
        where T : class, new()
    {
        var services = new ServiceCollection();
        if (validator is not null)
        {
            services.AddSingleton(validator);
        }

        var descriptor = new SettingsDescriptor<T>("test", SettingsScope.Household);
        try
        {
            await descriptor.ValidateAsync(settings, services.BuildServiceProvider(), TestContext.Current.CancellationToken);
            return true;
        }
        catch (ValidationException)
        {
            return false;
        }
    }
}
