```mermaid
sequenceDiagram
    autonumber
    actor U as User
    participant FE as sentium-portal (bell · /inbox)
    participant NC as Nuntius · NotificationsController
    participant SC as Nuntius · NotificationStreamController
    participant FD as NotificationFeed (in-process)

    FE->>SC: GET /api/nuntius/stream (SSE)
    SC->>FD: Subscribe(current user)
    SC-->>FE: data: { type: "connected" }
    FE->>NC: GET /api/nuntius/notifications/unread-count
    NC-->>FE: { count }
    loop Live events for this user only
        FD-->>SC: notification / read
        SC-->>FE: data: { type, data }
        FE->>NC: refetch unread count and list
    end
    U->>FE: Open a notification
    FE->>NC: POST /api/nuntius/notifications/{id}/read
    NC->>FD: Publish(user, "read") (updates other open sessions)
    Note over SC,FE: A heartbeat comment every 20 s; every reconnect sends "connected" again,<br/>so anything missed while offline is refetched
```
