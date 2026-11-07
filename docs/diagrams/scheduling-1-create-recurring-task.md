```mermaid
sequenceDiagram
    autonumber
    participant AG as AIAgent
    participant SG as Sentinel guard + approval
    participant ST as schedule_recurring_task
    participant SCH as Quartz Scheduler
    participant DB as PostgreSQL (praxis.qrtz_*)

    AG->>SG: call schedule_recurring_task({ JobName, CronExpression, Language, Code })
    SG->>SG: High-risk tool: policy check + user approval
    SG->>ST: ExecuteAsync (UserId + AgentName from the run context, never from the model)
    ST->>ST: Require a user; validate the cron (6-field, fires in future, >= 1 min apart, UTC)
    alt Rejected
        ST-->>AG: Error (no user / invalid / too frequent / quota reached)
    else Valid
        ST->>SCH: GetJobKeys(group = userId) - per-user quota
        ST->>SCH: ScheduleJob(JobDetail<ExecuteJob>, CronTrigger UTC, misfire = fire once)
        SCH->>DB: INSERT job, trigger, cron trigger
        SCH-->>ST: first fire time
        ST-->>AG: Success (schedule active, first run)
    end
```
