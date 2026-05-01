using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AgentRuntime.Infrastructure.Tools;

public sealed class DiagnosticToolDecorator(AIFunction innerFunction, ILogger logger) : AIFunction
{
    public override string Name => innerFunction.Name;
    public override string Description => innerFunction.Description;
    public override JsonSerializerOptions JsonSerializerOptions => innerFunction.JsonSerializerOptions;

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            var argsJson = JsonSerializer.Serialize(arguments, JsonSerializerOptions);
            logger.LogInformation("[Tool Call: {ToolName}] Arguments: {Args}", Name, argsJson);
        }

        try
        {
            var result = await innerFunction.InvokeAsync(arguments, cancellationToken);

            if (logger.IsEnabled(LogLevel.Information))
            {
                var resultString = result?.ToString() ?? "null";
                var preview = resultString.Length > 200 ? resultString[..200] + "..." : resultString;
                logger.LogInformation("[Tool Result: {ToolName}] Output: {Result}", Name, preview);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Tool Error: {ToolName}] Message: {Message}", Name, ex.Message);
            throw;
        }
    }
}
