using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
/// Sentium.Web has no authorization fallback policy (the SPA shell, health and docs endpoints must stay
/// anonymous), and since the gateway is gone nothing in front of it demands a session. Every controller
/// action therefore has to state its access rule itself: an <c>[Authorize*]</c> attribute on the action
/// or its controller, or an explicit <c>[AllowAnonymous]</c>.
/// </summary>
public sealed class ControllerAuthorizationTests
{
    private static readonly Assembly[] ControllerAssemblies =
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
        typeof(Sentium.Sandbox.Controllers.SandboxController).Assembly,
    ];

    [Fact]
    public void EveryControllerAction_DeclaresAnAuthorizationRule()
    {
        var actions = ControllerAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(IsController)
            .SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && !m.IsDefined(typeof(NonActionAttribute), inherit: true))
                .Select(m => (Controller: c, Action: m)))
            .ToList();

        actions.Should().NotBeEmpty("the scan must find the module controllers");

        var unprotected = actions
            .Where(x => !HasAuthorizationRule(x.Action) && !HasAuthorizationRule(x.Controller))
            .Select(x => $"{x.Controller.FullName}.{x.Action.Name}")
            .ToList();

        unprotected.Should().BeEmpty(
            "anonymous access must be opted into with [AllowAnonymous]; these actions declare no rule");
    }

    private static bool IsController(Type type)
    {
        return type is { IsClass: true, IsAbstract: false, IsNested: false }
        && typeof(ControllerBase).IsAssignableFrom(type)
        && !type.IsDefined(typeof(NonControllerAttribute), inherit: true);
    }

    private static bool HasAuthorizationRule(MemberInfo member) =>
        member.GetCustomAttributes(inherit: true).Any(a => a is IAuthorizeData or IAllowAnonymous);
}
