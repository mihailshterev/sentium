using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Infrastructure.Mvc;
using Sentium.Praxis;
using Xunit;

namespace Sentium.Tests.Unit.Infrastructure.Mvc;

/// <summary>
/// Module controllers are internal, which the default MVC discovery silently skips: without
/// <see cref="MvcBuilderExtensions.AddInternalControllers"/> every module route would 404.
/// </summary>
public sealed class InternalControllerDiscoveryTests
{
    private static readonly Assembly Praxis = typeof(PraxisModuleExtensions).Assembly;

    [Fact]
    public void AddInternalControllers_DiscoversEveryInternalModuleController()
    {
        var expected = Praxis.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsNested: false } && t.IsAssignableTo(typeof(ControllerBase)))
            .ToList();
        // module controllers are internal
        Assert.NotEmpty(expected);
        Assert.All(expected, t => Assert.True(!t.IsPublic));

        var discovered = DiscoverControllers(mvc => mvc.AddInternalControllers());

        Assert.Equivalent(expected, discovered);
    }

    [Fact]
    public void DefaultDiscovery_SkipsInternalControllers()
    {
        Assert.Empty(DiscoverControllers(_ => { }));
    }

    private static List<Type> DiscoverControllers(Action<IMvcBuilder> configure)
    {
        var mvc = new ServiceCollection().AddControllers();
        mvc.PartManager.ApplicationParts.Clear();
        mvc.AddApplicationPart(Praxis);
        configure(mvc);

        var feature = new ControllerFeature();
        mvc.PartManager.PopulateFeature(feature);
        return [.. feature.Controllers.Select(c => c.AsType())];
    }
}
