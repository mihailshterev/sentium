```mermaid
flowchart TD
    START([Squad run]) -->|Dynamic: no fixed plan| PL
    START -->|Custom: fixed plan, one step per workflow agent| ST

    PL["Planner<br/>Orchestrator control agent<br/>(JSON schema, roster names only)"]
    PL -->|SquadPlan with steps| ST
    PL -->|No work needed| SY

    ST["Step<br/>the step's specialist agent<br/>one compact message, fresh session,<br/>no approval-gated tools"]
    ST -->|StepOutput| CK

    CK{"Check<br/>deterministic checks, then the<br/>Validator control agent (JSON verdict)"}
    CK -->|Failed, not stuck, attempts left:<br/>retry once with the fixes as feedback| ST
    CK -->|Passed, or accepted as unverified:<br/>next step| ST
    CK -->|Last step done| SY

    SY["Synthesis<br/>single output or Summarizer synthesis<br/>+ structured risk assessment"]
    SY --> OUT([SquadResult: answer, risk, recommendation, steps])
```

Every hand-off between executors is one immutable, typed message (`StepRequest`, `StepOutput`,
`SquadOutcome`); there is no shared transcript. Each step's progress streams to the run's
`StreamRelay` stream while it runs.
