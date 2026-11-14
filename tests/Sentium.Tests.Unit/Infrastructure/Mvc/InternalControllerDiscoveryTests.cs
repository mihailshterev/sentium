using System.Reflection;
using FluentAssertions;
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
        expected.Should().NotBeEmpty().And.OnlyContain(t => !t.IsPublic, "module controllers are internal");

        var discovered = DiscoverControllers(mvc => mvc.AddInternalControllers());

        discovered.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void DefaultDiscovery_SkipsInternalControllers()
    {
        DiscoverControllers(_ => { }).Should().BeEmpty();
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
