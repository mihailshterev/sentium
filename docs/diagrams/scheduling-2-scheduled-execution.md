```mermaid
sequenceDiagram
    autonumber
    participant SCH as Quartz Scheduler
    participant EJ as Praxis · ExecuteJob
    participant SB as Sentium.Sandbox (/execute)
    participant SE as Sentinel (Sentium.Web)
    participant LOG as PostgreSQL (praxis.ExecutionLogs)

    SCH->>EJ: Trigger fires (on schedule)
    EJ->>EJ: Read JobData (code, language, agentId)
    EJ->>SB: SandboxClient - POST /execute (X-Internal-Token, correlationId)
    SB->>SE: Authorize the execution (policy endpoint)
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
    EJ->>LOG: Execution recorded by SandboxClient
```
