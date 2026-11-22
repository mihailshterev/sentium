```mermaid
sequenceDiagram
    autonumber
    actor U as User
    participant FE as sentium-portal (Vigil)
    participant WS as Vigil · VigilStatusController
    participant RT as GET /api/realtime/stream
    participant FD as VigilFeed (in-process)

    U->>FE: Open "Vigil"
    FE->>WS: GET /api/vigil/status (current snapshot)
    WS-->>FE: health of all targets
    Note over FE,RT: The realtime stream is already open (one per browser, opened at sign-in)
    loop Live events
        FD-->>RT: status change / incident / Device (topics "vigil.*")
        RT-->>FE: data: { topic, data }
        FE->>FE: patch the cached status, or refetch incidents
    end
    Note over RT,FE: A "ping" event every 15 s without events; a reconnect refetches everything shown
```
