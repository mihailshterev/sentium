using FluentValidation;

namespace Sentium.Tempus.Features.Tasks;

public sealed class SetTimelineTaskStatusRequestValidator : AbstractValidator<SetTimelineTaskStatusRequest>
{
    public SetTimelineTaskStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
