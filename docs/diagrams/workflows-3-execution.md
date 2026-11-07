```mermaid
sequenceDiagram
    autonumber
    participant Q as WorkflowRunQueue
    participant RP as WorkflowRunProcessor
    participant OR as WorkflowOrchestrator
    participant CW as DynamicCustomWorkflow
    participant SQ as SquadWorkflowRunner
    participant DB as PostgreSQL (praxis schema)
    participant SR as StreamRelay

    Q->>RP: WorkflowTrigger (bounded concurrency)
    RP->>SR: "Workflow started"
    RP->>OR: RunAsync(trigger) - per-attempt timeout, system scope
    OR->>CW: ExecuteAsync(trigger) (type = Custom)
    CW->>DB: Load the workflow and its ordered agents
    CW->>CW: Build the roster and a fixed plan (one step per agent)
    CW->>SQ: RunAsync(trigger, request, roster, plan)
    SQ-->>SR: each step's contribution, as it streams
    SQ-->>CW: SquadResult (answer, risk, recommendation, steps)
    CW-->>RP: WorkflowResult
    RP->>DB: Persist WorkflowRun (logs, explanation)
    RP->>SR: terminal frame "done"
    Note over RP,SR: Failures retry up to 4 attempts; timeout, user stop or shutdown persist<br/>[TIMEOUT] / [CANCELLED] / [FAILED] and emit "error" or "cancelled" instead
```
