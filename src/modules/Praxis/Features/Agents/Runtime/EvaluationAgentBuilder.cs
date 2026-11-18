using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sentium.Core.Results;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Models;
using Sentium.Praxis.Features.Skills;

namespace Sentium.Praxis.Features.Agents.Runtime;

/// <summary>
/// Freezes agents for Evaluations and builds them back from the snapshot (ADR 0003). The built agent runs on the
/// same base client and per-agent pipeline as chat agents, but its only context provider is skills and its only
/// tools are <see cref="EvaluationStubFunction"/>s: no Learnings, stored history, workspace files, approvals or
/// Sentinel.
/// </summary>
internal sealed class EvaluationAgentBuilder(
    [FromKeyedServices(PraxisModuleExtensions.AgentChatClientKey)] IChatClient baseChatClient,
    IAgentRoster roster,
    IAgentFactory agentFactory,
    IModelCatalog modelCatalog,
    IModelContextWindowProvider contextWindows,
    IAgentSkillRepository skillRepository,
    AgentSkillsProviderFactory skillsProviderFactory,
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory) : IEvaluationAgentBuilder
{
    /// <summary>
    /// The MEAI quality evaluators cap the Judge at 800 output tokens. llama-server counts a model's thinking against
    /// that cap, so a reasoning Judge (Gemma 4) runs out before it writes its score: replies come back empty or cut off
    /// mid-reasoning. Judge prompts are short, so a larger cap costs little.
    /// </summary>
    private const int JudgeMaxOutputTokens = 4096;

    public async Task<Result<EvaluationAgentSnapshot>> SnapshotAsync(string agentName, string? model, Guid userId, CancellationToken ct = default)
    {
        var entry = await roster.FindAsync(agentName, userId, ct);
        if (entry is null)
        {
            return Result<EvaluationAgentSnapshot>.Invalid($"You have no agent named '{agentName}'.");
        }

        var blueprint = await agentFactory.ResolveBlueprintAsync(entry.Name, model, userId, allowApprovalTools: true, ct);
        if (blueprint is null)
        {
            return Result<EvaluationAgentSnapshot>.Invalid($"You have no agent named '{agentName}'.");
        }

        if (await ModelErrorAsync(blueprint.Model, ct) is { } modelError)
        {
            return Result<EvaluationAgentSnapshot>.Invalid(modelError);
        }

        var userSkills = await skillRepository.GetForUserAsync(userId, ct);

        return Result<EvaluationAgentSnapshot>.Success(new EvaluationAgentSnapshot(
            entry.Name,
            blueprint.Model,
            blueprint.ComposeInstructions(),
            blueprint.Temperature,
            [.. blueprint.Tools.OfType<AIFunction>().Select(f => new EvaluationToolDeclaration(f.Name, f.Description, f.JsonSchema))],
            [.. userSkills.Select(s => new EvaluationSkill(s.Name, s.Description, s.Instructions))]));
    }

    public async Task<AIAgent> CreateAsync(EvaluationAgentSnapshot snapshot, EvaluationToolStub stub, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(stub);

        var contextWindow = await contextWindows.GetContextWindowAsync(snapshot.Model, ct);

        var chatOptions = new ChatOptions
        {
            ModelId = snapshot.Model,
            Instructions = snapshot.Instructions,
            Tools = [.. snapshot.Tools.Select(t => new EvaluationStubFunction(t, stub))],
            Temperature = snapshot.Temperature,
        };

        var options = new ChatClientAgentOptions
        {
            Name = snapshot.AgentName,
            ChatOptions = chatOptions,
            AIContextProviders = [skillsProviderFactory.CreateFrozen(snapshot.UserSkills)],
        };

        var chatClient = AgentChatPipeline.Build(baseChatClient, contextWindow, loggerFactory, serviceProvider);

        return new ChatClientAgent(chatClient, options, loggerFactory, serviceProvider);
    }

    public async Task<Result<string>> ResolveModelAsync(string? model, Guid userId, CancellationToken ct = default)
    {
        var resolved = await agentFactory.ResolveModelAsync(model, userId, ct);
        return await ModelErrorAsync(resolved, ct) is { } error ? Result<string>.Invalid(error) : Result<string>.Success(resolved);
    }

    public async Task<IReadOnlySet<string>?> ListModelsAsync(CancellationToken ct = default)
    {
        var models = await modelCatalog.ListAsync(ct);
        return models?.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
    }

    public IChatClient CreateJudgeClient(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        return new ChatClientBuilder(baseChatClient)
            .ConfigureOptions(options =>
            {
                options.ModelId = model;
                options.MaxOutputTokens = Math.Max(options.MaxOutputTokens ?? 0, JudgeMaxOutputTokens);
            })
            .Build(serviceProvider);
    }

    private async Task<string?> ModelErrorAsync(string model, CancellationToken ct)
    {
        var models = await modelCatalog.ListAsync(ct);
        if (models is null)
        {
            return "The model catalog is unavailable, so the model cannot be checked. Try again shortly.";
        }

        return models.Any(m => string.Equals(m.Id, model, StringComparison.Ordinal)) ? null : $"Unknown model '{model}'.";
    }
}
