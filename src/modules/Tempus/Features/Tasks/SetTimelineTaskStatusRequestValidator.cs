using FluentValidation;

namespace Sentium.Tempus.Features.Tasks;

internal sealed class SetTimelineTaskStatusRequestValidator : AbstractValidator<SetTimelineTaskStatusRequest>
{
    public SetTimelineTaskStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
