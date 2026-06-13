```mermaid
sequenceDiagram
    autonumber
    participant SCH as Quartz Scheduler
    participant EJ as ExecuteJob
    participant SB as Sandbox (/sandbox/execute)
    participant SE as Sentinel (PDP)

    SCH->>EJ: Trigger fires (on schedule)
    EJ->>EJ: Read JobData (code, language, agentId)
    EJ->>SB: POST /sandbox/execute (X-Internal-Token, correlationId)
    SB->>SE: Authorize the execution (PDP)
    alt Allowed
        SE-->>SB: Allowed
        SB->>SB: Execute in a Docker container
        SB-->>EJ: result (exit code, output)
        EJ->>EJ: Log success / warning
    else Denied
        SE-->>SB: Denied
        SB-->>EJ: PolicyDenied = true
        EJ->>EJ: Log policy denial
    end
```
