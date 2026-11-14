using FluentValidation;

namespace Sentium.Mcp.Features.Servers;

/// <remarks>
/// The transport cannot change after creation and is not part of the request, so the transport-specific requirements
/// (an endpoint vs. an image, header vs. environment-variable secret names) are checked by the service against the
/// stored server (<see cref="McpRequestRules.ValidateForTransport"/>); this validator checks the shapes.
/// </remarks>
internal sealed class UpdateMcpServerRequestValidator : AbstractValidator<UpdateMcpServerRequest>
{
    public UpdateMcpServerRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);

        When(x => !string.IsNullOrWhiteSpace(x.Endpoint), () => RuleFor(x => x.Endpoint).McpEndpoint());
        When(x => !string.IsNullOrWhiteSpace(x.Image), () => RuleFor(x => x.Image).McpImage());

        RuleFor(x => x.Args).McpArgs();
        RuleFor(x => x.Network).McpNetwork();

        RuleFor(x => x.MemoryMb).InclusiveBetween(32, 8_192).When(x => x.MemoryMb.HasValue);
        RuleFor(x => x.CpuLimit).InclusiveBetween(0.1, 8).When(x => x.CpuLimit.HasValue);
    }
}
