using System.Text.RegularExpressions;
using FluentValidation;
using Sentium.Memoria.Common;

namespace Sentium.Memoria.Features.Sync;

internal static partial class SyncRules
{
    public const string PathMessage = "Each path must be a vault-relative .md path without '..' segments.";

    public static IRuleBuilderOptions<T, string> MustBeNotePath<T>(this IRuleBuilder<T, string> rule)
    {
        return rule.Must(p => VaultPaths.Normalize(p) is not null).WithMessage(PathMessage);
    }

    public static IRuleBuilderOptions<T, string> MustBeHash<T>(this IRuleBuilder<T, string> rule)
    {
        return rule.Must(h => h is not null && Sha256Hex().IsMatch(h)).WithMessage("Hash must be a hex SHA-256.");
    }

    [GeneratedRegex("^[0-9a-fA-F]{64}$")]
    private static partial Regex Sha256Hex();
}
