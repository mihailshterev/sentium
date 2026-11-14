using FluentValidation;

namespace Sentium.Mcp.Features.Servers;

public sealed class UpdateMcpToolsRequestValidator : AbstractValidator<UpdateMcpToolsRequest>
{
    public UpdateMcpToolsRequestValidator()
    {
        RuleFor(x => x.Tools).NotNull().Must(t => t.Count <= 500).WithMessage("Too many tool updates.");
        RuleForEach(x => x.Tools).ChildRules(tool => tool.RuleFor(t => t.Name).NotEmpty().MaximumLength(256));
    }
}
