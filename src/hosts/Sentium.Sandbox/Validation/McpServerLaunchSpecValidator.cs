using FluentValidation;
using Sentium.Sandbox.Contracts;

namespace Sentium.Sandbox.Validation;

internal sealed class McpServerLaunchSpecValidator : AbstractValidator<McpServerLaunchSpec>
{
    public McpServerLaunchSpecValidator()
    {
        RuleFor(x => x.Image).NotEmpty().MaximumLength(512);

        RuleFor(x => x.Args)
            .Must(args => args is null || (args.Count <= 64 && args.All(a => a is not null && a.Length <= 1024)))
            .WithMessage("At most 64 arguments of up to 1024 characters each.");

        RuleFor(x => x.Env)
            .Must(env => env is null || (env.Count <= 32 && env.All(e =>
                System.Text.RegularExpressions.Regex.IsMatch(e.Key, "^[A-Za-z_][A-Za-z0-9_]{0,127}$")
                && e.Value is not null
                && e.Value.Length <= 8_192
                && !e.Value.Contains('\0', StringComparison.Ordinal))))
            .WithMessage("At most 32 environment variables with valid names and values of up to 8192 characters.");

        RuleFor(x => x.Network).MaximumLength(128);
        RuleFor(x => x.MemoryMb).InclusiveBetween(32, 8_192).When(x => x.MemoryMb.HasValue);
        RuleFor(x => x.CpuLimit).InclusiveBetween(0.1, 8).When(x => x.CpuLimit.HasValue);
    }
}
