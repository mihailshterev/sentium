using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sentium.Codex;
using Sentium.Identity;
using Sentium.Locus;
using Sentium.Mcp;
using Sentium.Memoria;
using Sentium.Nuntius;
using Sentium.Praxis;
using Sentium.Probatio;
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
        typeof(MemoriaModuleExtensions).Assembly,
        typeof(PraxisModuleExtensions).Assembly,
        typeof(SchedulingModuleExtensions).Assembly,
        typeof(McpModuleExtensions).Assembly,
        typeof(ProbatioModuleExtensions).Assembly,
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

        var failing = TypeDependencyScanner.TypesDependingOnAny(module, module.GetTypes().Where(IsController), dbContexts);

        Assert.True(
            failing.Count == 0,
            $"{moduleName} controllers must go through services, not a DbContext: {string.Join(", ", failing)}");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Entities_DoNotDependOnFeaturesControllersOrData(string moduleName)
    {
        var module = Module(moduleName);
        var upperLayers = TypeNames(module, t => IsController(t) || InLayer(t, ".Features") || InLayer(t, ".Data"))
            .Where(name => !EntityFeatureTypeAllowList.Contains(name))
            .ToArray();

        var failing = TypeDependencyScanner.TypesDependingOnAny(module, Entities(module), upperLayers);

        Assert.True(
            failing.Count == 0,
            $"{moduleName} entities must not reach up into features, controllers or data: {string.Join(", ", failing)}");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void NothingDependsOnControllers(string moduleName)
    {
        var module = Module(moduleName);
        var controllers = TypeNames(module, IsController);

        if (controllers.Length == 0)
        {
            return;
        }

        var others = module.GetTypes().Where(t => !IsController(t) && !t.Name.Contains('<', StringComparison.Ordinal));
        var failing = TypeDependencyScanner.TypesDependingOnAny(module, others, controllers);

        Assert.True(
            failing.Count == 0,
            $"{moduleName}: controllers are the HTTP edge, no other type may depend on them: {string.Join(", ", failing)}");
    }

    [Fact]
    public void LayerScan_FindsKnownTypes()
    {
        // Guards against the rules above passing vacuously.
        var praxis = typeof(PraxisModuleExtensions).Assembly;

        Assert.NotEmpty(TypeNames(praxis, IsController));
        Assert.NotEmpty(TypeNames(praxis, t => t.IsAssignableTo(typeof(DbContext))));
        Assert.NotEmpty(TypeNames(praxis, t => InLayer(t, ".Features")));
        Assert.NotEmpty(Entities(praxis));

        // And that the entity rule can fail: AgentSkill really embeds a Features type, which is only allow-listed.
        var withoutAllowList = TypeDependencyScanner.TypesDependingOnAny(
            praxis,
            Entities(praxis),
            ["Sentium.Praxis.Features.Skills.AgentSkillType"]);

        Assert.NotEmpty(withoutAllowList);
    }

    private static Assembly Module(string name) => Modules.Single(m => m.GetName().Name == name);

    private static bool IsController(Type type) => type.IsAssignableTo(typeof(ControllerBase));

    private static IEnumerable<Type> Entities(Assembly module)
    {
        return module.GetTypes().Where(t => t.Namespace?.EndsWith(".Entities", StringComparison.Ordinal) == true);
    }

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
