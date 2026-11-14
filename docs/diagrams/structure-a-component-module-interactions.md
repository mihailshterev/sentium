```mermaid
flowchart TB
    Portal["sentium-portal<br/>(React SPA, incl. login page)"]

    subgraph WEB["Sentium.Web (modular monolith, one process)"]
        ID["Identity<br/>(users · roles · session)"]
        REG["Registry<br/>(configuration)"]
        SEN["Sentinel<br/>(Policy Decision Point)"]
        CDX["Codex<br/>(RAG · knowledge base)"]
        PRX["Praxis<br/>(agents · orchestration · workflows)"]
        MCP["Mcp<br/>(MCP servers · tool catalogue)"]
        SCH["Scheduling<br/>(Quartz · recurring jobs)"]
        VIG["Vigil<br/>(health monitoring)"]
    end

    SBX["Sentium.Sandbox<br/>(isolated execution, stateless)"]

    Portal -->|"HTTPS + HttpOnly cookie<br/>/api/{module}"| WEB

    PRX -->|"Contracts: settings"| REG
    PRX -->|"Contracts: policy evaluation"| SEN
    PRX -->|"Contracts: ingest · search"| CDX
    MCP -->|"Contracts: agent tools"| PRX
    MCP -->|"Contracts: settings"| REG
    SCH -->|"Contracts: agent tools · sandbox"| PRX
    SEN -->|"Contracts: settings"| REG
    VIG -->|"Contracts: settings"| REG

    PRX -->|"POST /execute<br/>(X-Internal-Token)"| SBX
    SBX -->|"POST /api/sentinel/policy/evaluate<br/>(X-Internal-Token)"| SEN

    VIG -.->|"in-process health checks"| ID
    VIG -.->|"in-process health checks"| SEN
    VIG -.->|"in-process health checks"| PRX
    VIG -.->|"in-process health checks"| CDX
    VIG -.->|"in-process health checks"| REG
    VIG -.->|"HTTP /health"| SBX
```

Solid arrows inside Sentium.Web are in-process calls through the target module's `Contracts`
(`ModuleBoundaryTests` rejects any other dependency). Only the two Sandbox arrows cross a process
boundary.
