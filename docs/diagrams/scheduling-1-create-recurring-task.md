```mermaid
sequenceDiagram
    autonumber
    participant AG as AIAgent
    participant ST as schedule_recurring_task
    participant SF as ISchedulerFactory (Quartz)
    participant SCH as Quartz Scheduler

    AG->>ST: ExecuteAsync({ JobName, CronExpression, Language, Code })
    ST->>ST: Validate the cron expression
    alt Invalid cron
        ST-->>AG: Error (syntactically invalid expression)
    else Valid cron
        ST->>SF: GetScheduler()
        ST->>SCH: ScheduleJob(JobDetail<ExecuteJob>, CronTrigger)
        SCH-->>ST: registered
        ST-->>AG: Success (schedule active)
    end
```
