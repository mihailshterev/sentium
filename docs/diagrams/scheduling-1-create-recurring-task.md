```mermaid
sequenceDiagram
    autonumber
    actor U as User
    participant AG as AIAgent
    participant SM as SentinelFunctionMiddleware
    participant ST as schedule_recurring_task
    participant SCH as Quartz Scheduler
    participant DB as PostgreSQL (praxis.qrtz_*)

    AG-->>U: Approval request (high-risk tool)
    U->>AG: Approve
    AG->>SM: Invoke({ jobName, cronExpression, code, language })
    SM->>SM: Sentinel policy check (Code / Execute, script forwarded as payload)
    SM->>ST: ScheduleAsync - user from the run context, agent from AIAgent.CurrentRunContext (never from the model)
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
