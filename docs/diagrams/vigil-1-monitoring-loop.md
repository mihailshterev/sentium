```mermaid
sequenceDiagram
    autonumber
    participant MW as MonitoringWorker (background)
    participant RS as Registry settings
    participant PR as Health probes
    participant ST as State store
    participant IN as Incident store
    participant FD as VigilFeed (in-process)

    Note over PR: Modules - in-process health checks by tag (Identity, Sentinel, Praxis, Codex, Registry)<br/>Sandbox - HTTP /health · Ollama - HTTP /api/version · PostgreSQL, Redis - TCP
    loop Every PollInterval
        MW->>RS: GetAsync() - cadence and thresholds
        par For each target
            MW->>PR: ProbeAsync() (with timeout)
            PR-->>MW: status + latency
            MW->>MW: Mark Degraded on high latency
            MW->>ST: UpdateStatus(result)
            MW->>IN: Evaluate incident (open/resolve)
            opt Status changed
                MW->>FD: Publish("status")
            end
            opt Incident opened/resolved
                MW->>FD: Publish("incident.opened" / "incident.resolved")
            end
        end
    end
```
