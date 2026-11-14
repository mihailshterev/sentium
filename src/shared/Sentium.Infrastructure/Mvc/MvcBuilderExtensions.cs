using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace Sentium.Infrastructure.Mvc;

public static class MvcBuilderExtensions
{
    /// <summary>
    /// Replaces the default controller discovery with one that also finds <c>internal</c> controllers.
    /// </summary>
    public static IMvcBuilder AddInternalControllers(this IMvcBuilder mvc)
    {
        ArgumentNullException.ThrowIfNull(mvc);

        return mvc.ConfigureApplicationPartManager(manager =>
        {
            var defaults = manager.FeatureProviders.OfType<ControllerFeatureProvider>().ToList();
            foreach (var provider in defaults)
            {
                manager.FeatureProviders.Remove(provider);
            }

            manager.FeatureProviders.Add(new InternalControllerFeatureProvider());
        });
    }
}
