using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetArchTest.Rules;
using Sentium.Codex;
using Sentium.Identity;
using Sentium.Locus;
using Sentium.Mcp;
using Sentium.Nuntius;
using Sentium.Praxis;
using Sentium.Registry;
using Sentium.Scheduling;
using Sentium.Sentinel;
using Sentium.Tempus;
using Sentium.Vigil;
using Xunit;

namespace Sentium.Tests.Unit.Modules;

/// <summary>
/// Layering rules inside a single module. They are written against type kinds (controllers, DbContexts) and the
/// stable <c>.Entities</c> / <c>.Features</c> / <c>.Data</c> namespaces, not against folders, so they keep holding
/// when a feature's controller, DTOs and services are co-located under <c>Features/&lt;Name&gt;</c>.
/// </summary>
public sealed class ModuleLayeringTests
{
    private static readonly Assembly[] Modules =
    [
        typeof(IdentityModuleExtensions).Assembly,
        typeof(RegistryModuleExtensions).Assembly,
        typeof(NuntiusModuleExtensions).Assembly,
        typeof(SentinelModuleExtensions).Assembly,
        typeof(CodexModuleExtensions).Assembly,
        typeof(LocusModuleExtensions).Assembly,
        typeof(TempusModuleExtensions).Assembly,
        typeof(PraxisModuleExtensions).Assembly,
        typeof(SchedulingModuleExtensions).Assembly,
        typeof(McpModuleExtensions).Assembly,
        typeof(VigilModuleExtensions).Assembly,
    ];

    /// <summary>
    /// Value types that entities legitimately embed (enum columns, JSON column payloads) but that still live in a
    /// Features namespace. Moving them next to the entities would let this list shrink; it must never grow.
    /// </summary>
    private static readonly HashSet<string> EntityFeatureTypeAllowList =
    [
        "Sentium.Praxis.Features.Skills.AgentSkillType",
        "Sentium.Praxis.Features.Files.FileProcessingStatus",
        "Sentium.Registry.Features.Settings.SettingsContainer",
        "Sentium.Praxis.Features.Workflows.WorkflowLogEntry",
    ];

    public static TheoryData<string> ModuleNames() => [.. Modules.Select(m => m.GetName().Name!)];

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Controllers_DoNotDependOnDbContexts(string moduleName)
    {
        var module = Module(moduleName);
        var dbContexts = TypeNames(module, t => t.IsAssignableTo(typeof(DbContext)));

        if (dbContexts.Length == 0)
        {
            return;
        }

        var result = Types.InAssembly(module)
            .That().Inherit(typeof(ControllerBase))
            .ShouldNot().HaveDependencyOnAny(dbContexts)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "{0} controllers must go through services, not a DbContext: {1}",
            moduleName,
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Entities_DoNotDependOnFeaturesControllersOrData(string moduleName)
    {
        var module = Module(moduleName);
        var upperLayers = TypeNames(module, t =>
                t.IsAssignableTo(typeof(ControllerBase)) || InLayer(t, ".Features") || InLayer(t, ".Data"))
            .Where(name => !EntityFeatureTypeAllowList.Contains(name))
            .ToArray();

        var result = Types.InAssembly(module)
            .That().ResideInNamespaceEndingWith(".Entities")
            .ShouldNot().HaveDependencyOnAny(upperLayers)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "{0} entities must not reach up into features, controllers or data: {1}",
            moduleName,
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void NothingDependsOnControllers(string moduleName)
    {
        var module = Module(moduleName);
        var controllers = TypeNames(module, t => t.IsAssignableTo(typeof(ControllerBase)));

        if (controllers.Length == 0)
        {
            return;
        }

        var result = Types.InAssembly(module)
            .That().DoNotInherit(typeof(ControllerBase))
            .And().DoNotHaveNameMatching("<")
            .ShouldNot().HaveDependencyOnAny(controllers)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "{0}: controllers are the HTTP edge, no other type may depend on them: {1}",
            moduleName,
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void LayerScan_FindsKnownTypes()
    {
        // Guards against the rules above passing vacuously.
        var praxis = typeof(PraxisModuleExtensions).Assembly;

        TypeNames(praxis, t => t.IsAssignableTo(typeof(ControllerBase))).Should().NotBeEmpty();
        TypeNames(praxis, t => t.IsAssignableTo(typeof(DbContext))).Should().NotBeEmpty();
        TypeNames(praxis, t => InLayer(t, ".Features")).Should().NotBeEmpty();
        Types.InAssembly(praxis).That().ResideInNamespaceEndingWith(".Entities").GetTypes().Should().NotBeEmpty();

        // And that the entity rule can fail: AgentSkill really embeds a Features type, which is only allow-listed.
        var withoutAllowList = Types.InAssembly(praxis)
            .That().ResideInNamespaceEndingWith(".Entities")
            .ShouldNot().HaveDependencyOnAny("Sentium.Praxis.Features.Skills.AgentSkillType")
            .GetResult();

        withoutAllowList.IsSuccessful.Should().BeFalse();
    }

    private static Assembly Module(string name) => Modules.Single(m => m.GetName().Name == name);

    private static string[] TypeNames(Assembly module, Func<Type, bool> predicate)
    {
        return module.GetTypes()
            .Where(t => t.FullName is not null && !t.FullName.Contains('<', StringComparison.Ordinal))
            .Where(predicate)
            .Select(t => t.FullName!)
            .ToArray();
    }

    private static bool InLayer(Type type, string layer)
    {
        return type.Namespace is not null
        && (type.Namespace.EndsWith(layer, StringComparison.Ordinal)
            || type.Namespace.Contains(layer + ".", StringComparison.Ordinal));
    }
}
