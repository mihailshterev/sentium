using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Sentium.Codex;
using Sentium.Identity;
using Sentium.Locus;
using Sentium.Mcp;
using Sentium.Praxis;
using Sentium.Registry;
using Sentium.Scheduling;
using Sentium.Sentinel;
using Sentium.Tempus;
using Sentium.Vigil;
using Xunit;

namespace Sentium.Tests.Unit.Modules;

/// <summary>
/// Modules may only depend on another module's <c>Sentium.&lt;Module&gt;.Contracts</c> surface,
/// never on its implementation types (services, DbContexts, controllers, ...).
/// </summary>
public sealed class ModuleBoundaryTests
{
    private static readonly Assembly[] Modules =
    [
        typeof(IdentityModuleExtensions).Assembly,
        typeof(RegistryModuleExtensions).Assembly,
        typeof(SentinelModuleExtensions).Assembly,
        typeof(CodexModuleExtensions).Assembly,
        typeof(LocusModuleExtensions).Assembly,
        typeof(TempusModuleExtensions).Assembly,
        typeof(PraxisModuleExtensions).Assembly,
        typeof(SchedulingModuleExtensions).Assembly,
        typeof(McpModuleExtensions).Assembly,
        typeof(VigilModuleExtensions).Assembly,
    ];

    public static TheoryData<string> ModuleNames() => [.. Modules.Select(m => m.GetName().Name!)];

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_DependsOnlyOnOtherModulesContracts(string moduleName)
    {
        var module = Modules.Single(m => m.GetName().Name == moduleName);

        var forbidden = Modules
            .Where(other => other != module)
            .SelectMany(ImplementationTypeNames)
            .ToArray();

        var result = Types.InAssembly(module)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "{0} must reach other modules only through their Contracts, but these types do not: {1}",
            moduleName,
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void DependencyScan_DetectsKnownContractDependency()
    {
        // Guards against the boundary test passing vacuously: Praxis really does use the Registry contract.
        var result = Types.InAssembly(typeof(PraxisModuleExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Sentium.Registry.Contracts.ISettingsService")
            .GetResult();

        result.IsSuccessful.Should().BeFalse();
    }

    private static IEnumerable<string> ImplementationTypeNames(Assembly module)
    {
        var contractsNamespace = $"{module.GetName().Name}.Contracts";

        return module.GetTypes()
            .Where(t => t.FullName is not null && !t.FullName.Contains('<', StringComparison.Ordinal))
            .Where(t => t.Namespace is null || !(t.Namespace == contractsNamespace || t.Namespace.StartsWith(contractsNamespace + ".", StringComparison.Ordinal)))
            .Select(t => t.FullName!);
    }
}
