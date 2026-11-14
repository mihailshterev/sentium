using FluentValidation;
using Sentium.Mcp.Entities;

namespace Sentium.Mcp.Features.Servers;

public sealed class CreateMcpServerRequestValidator : AbstractValidator<CreateMcpServerRequest>
{
    public CreateMcpServerRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(McpNames.IsValidServerName)
            .WithMessage("Name must be 2-24 characters: lowercase letters, digits and '-', starting with a letter or digit.");

        RuleFor(x => x.Transport).IsInEnum();

        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);

        When(x => x.Transport == McpTransport.Http, () =>
            RuleFor(x => x.Endpoint).McpEndpoint());

        When(x => x.Transport == McpTransport.SandboxStdio, () =>
        {
            RuleFor(x => x.Image).McpImage();
            RuleFor(x => x.Args).McpArgs();
            RuleFor(x => x.Network).McpNetwork();
        });

        RuleFor(x => x.MemoryMb).InclusiveBetween(32, 8_192).When(x => x.MemoryMb.HasValue);
        RuleFor(x => x.CpuLimit).InclusiveBetween(0.1, 8).When(x => x.CpuLimit.HasValue);

        RuleFor(x => x.Secrets)
            .Must((request, secrets) => McpRequestRules.SecretsAreValid(request.Transport, secrets?.Select(s => KeyValuePair.Create(s.Key, (string?)s.Value))))
            .WithMessage(x => McpRequestRules.SecretsMessage(x.Transport));

        RuleFor(x => x.Variables)
            .Must((request, variables) => McpRequestRules.SecretsAreValid(request.Transport, variables?.Select(s => KeyValuePair.Create(s.Key, (string?)s.Value))))
            .WithMessage(x => McpRequestRules.SecretsMessage(x.Transport));

        RuleFor(x => x)
            .Must(x => !McpRequestRules.NamesOverlap(x.Secrets?.Keys, x.Variables?.Keys))
            .WithMessage("A name is either a secret or a plain variable, not both.");
    }
}
