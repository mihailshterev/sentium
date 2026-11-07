```mermaid
sequenceDiagram
    autonumber
    participant C as Caller (controller / workflow)
    participant F as CompositeAgentFactory
    participant RS as RegistrySettingsService
    participant CW as ModelContextWindowProvider
    participant TP as AgentToolProvider
    participant AI as AgentInstructions
    participant P as AgentChatPipeline

    C->>F: CreateAsync(agentName, overrideModel, chatHistory)
    F->>F: Resolve definition (native IAgent or the user's custom agent)
    F->>RS: GetAsync(userId) - harness, default model, temperature
    F->>CW: GetContextWindowAsync(model) - per-slot n_ctx from the model catalog (cached)
    F->>TP: GetToolsForAgent(agentName)
    F->>F: Keep the agent's curated tools (drop approval tools for unattended runs)
    F->>AI: Compose(harness, role, tools) - one system prompt
    F->>P: Build(shared base client, context window)
    Note over P: FunctionInvocation (max 10 rounds) -> Compaction -> OpenTelemetry -> llama-server (OpenAI API)
    F->>F: ChatClientAgent + context providers (skills, learnings) + chat history
    F->>F: .AsBuilder().Use(SentinelFunctionMiddleware)
    F-->>C: Ready AIAgent
```
