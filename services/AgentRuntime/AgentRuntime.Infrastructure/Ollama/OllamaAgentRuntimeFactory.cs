using AgentRuntime.Core.Agents;
using AgentRuntime.Infrastructure.Agents;
using OllamaSharp;

namespace AgentRuntime.Infrastructure.Ollama;

public sealed class OllamaAgentRuntimeFactory : IAgentRuntimeFactory
{
    private readonly Uri Endpoint;

    public OllamaAgentRuntimeFactory(Uri endpoint)
    {
        Endpoint = endpoint;
    }

    public IAgentRuntime Create(AgentRole role)
    {
        var client = new OllamaApiClient(Endpoint)
        {
            SelectedModel = role switch
            {
                AgentRole.Summarizer => "gemma2:2b",
                AgentRole.SecurityAnalyst => "llama3.1:8b",
                AgentRole.Planner => "qwen2.5:14b",
                _ => "mistral:7b"
            }
        };

        return new OllamaAgentRuntime(client);
    }
}
