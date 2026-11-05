```mermaid
sequenceDiagram
    autonumber
    actor U as User
    participant FE as sentium-portal (Sandbox)
    participant SC as Praxis · SandboxController
    participant LOG as PostgreSQL (praxis.ExecutionLogs)
    participant BL as Blob Storage (Azurite)

    U->>FE: Open the execution list
    FE->>SC: GET /api/sandbox/executions (page, filters)
    SC->>LOG: GetPagedAsync(query)
    LOG-->>SC: records + total count
    SC-->>FE: PagedResponse
    U->>FE: Download an artifact
    FE->>SC: GET /api/sandbox/artifacts/{path}
    SC->>BL: Read the blob (stream)
    SC-->>FE: file (content-type)
```
