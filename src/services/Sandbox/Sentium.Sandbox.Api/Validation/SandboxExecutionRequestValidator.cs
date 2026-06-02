using FluentValidation;
using Microsoft.Extensions.Options;
using Sentium.Sandbox.Api.Dtos;
using Sentium.Sandbox.Application.Options;
using Sentium.Sandbox.Core.Models;

namespace Sentium.Sandbox.Api.Validation;

public sealed class SandboxExecutionRequestValidator : AbstractValidator<SandboxExecutionRequest>
{
    public SandboxExecutionRequestValidator(IOptions<SandboxOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        var opts = options.Value;

        RuleFor(x => x.Language)
            .NotEmpty()
            .Must(lang => Enum.TryParse<ExecutionLanguage>(lang, ignoreCase: true, out _))
            .WithMessage(x => $"Unknown language '{x.Language}'. Valid values: {string.Join(", ", Enum.GetNames<ExecutionLanguage>())}");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("'Code' must not be empty.")
            .MaximumLength(opts.MaxCodeSizeBytes)
            .WithMessage($"Code exceeds the maximum allowed size of {opts.MaxCodeSizeBytes:N0} bytes.");

        RuleFor(x => x.AgentId)
            .NotEmpty().WithMessage("'AgentId' must not be empty.");

        RuleFor(x => x.FileContext)
            .Must(fc => fc == null || fc.Count <= opts.MaxFileContextEntries)
            .WithMessage($"FileContext exceeds the maximum of {opts.MaxFileContextEntries} entries.");

        RuleForEach(x => x.FileContext)
            .ChildRules(file =>
            {
                file.RuleFor(f => f.FileName)
                    .NotEmpty().WithMessage("A FileContext entry has an empty FileName.");

                file.RuleFor(f => f.Content)
                    .NotEmpty().WithMessage("FileContext entry has empty Content.")
                    .MaximumLength(opts.MaxFileContentBytes)
                    .WithMessage($"FileContext entry exceeds the maximum content size of {opts.MaxFileContentBytes:N0} bytes.");
            });
    }
}
