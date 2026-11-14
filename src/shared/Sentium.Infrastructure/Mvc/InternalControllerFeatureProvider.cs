using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Sentium.Infrastructure.Mvc;

/// <summary>
/// Discovers controllers the same way as the default <see cref="ControllerFeatureProvider"/> but also accepts
/// <c>internal</c> ones, so a module's controllers, DTOs and services can stay internal to its assembly.
/// </summary>
internal sealed class InternalControllerFeatureProvider : ControllerFeatureProvider
{
    private const string ControllerSuffix = "Controller";

    protected override bool IsController(TypeInfo typeInfo)
    {
        if (!typeInfo.IsClass || typeInfo.IsAbstract || typeInfo.IsNested || typeInfo.ContainsGenericParameters)
        {
            return false;
        }

        if (typeInfo.IsDefined(typeof(NonControllerAttribute)))
        {
            return false;
        }

        return typeInfo.Name.EndsWith(ControllerSuffix, StringComparison.OrdinalIgnoreCase)
            || typeInfo.IsDefined(typeof(ControllerAttribute));
    }
}
