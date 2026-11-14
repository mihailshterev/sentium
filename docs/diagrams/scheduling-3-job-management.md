```mermaid
sequenceDiagram
    autonumber
    actor U as User
    participant FE as sentium-portal (Scheduler)
    participant SC as Scheduling · SchedulerController
    participant SCH as Quartz Scheduler

    U->>FE: Open "Scheduler"
    FE->>SC: GET /api/praxis/scheduler/jobs
    SC->>SCH: GetJobKeys(group = caller's userId; any group for a Sovereign)
    SCH-->>SC: jobs (cron, next/prev run, status)
    SC-->>FE: list tagged with AgentId
    U->>FE: Delete a job
    FE->>SC: DELETE /api/praxis/scheduler/agents/{agentId}/jobs/{jobId}
    SC->>SCH: Find job in the caller's group (any group for a Sovereign)
    alt Not visible to the caller or agent mismatch
        SC-->>FE: 404 Not Found
    else Owned
        SC->>SCH: DeleteJob(jobKey)
        SC-->>FE: 204 No Content
    end
```
