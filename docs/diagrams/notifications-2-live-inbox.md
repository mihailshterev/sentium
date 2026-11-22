```mermaid
sequenceDiagram
    autonumber
    actor U as User
    participant FE as sentium-portal (bell · /inbox)
    participant NC as Nuntius · NotificationsController
    participant RT as GET /api/realtime/stream
    participant FD as NotificationFeed (in-process)

    FE->>RT: open the realtime stream (SSE, one per browser)
    RT-->>FE: data: { topic: "connected" }
    FE->>NC: GET /api/nuntius/notifications/unread-count
    NC-->>FE: { count }
    loop Live events for this user only
        FD-->>RT: notification / read (topic "notifications")
        RT-->>FE: data: { topic, data: { type, data } }
        FE->>NC: refetch unread count and list
    end
    U->>FE: Open a notification
    FE->>NC: POST /api/nuntius/notifications/{id}/read
    NC->>FD: Publish(user, "read") (updates other open sessions)
    Note over RT,FE: A "ping" event every 15 s; every reconnect sends "connected" again,<br/>so anything missed while offline is refetched
    Note over FD: Paired Devices keep their own stream, /api/nuntius/devices/stream
```
