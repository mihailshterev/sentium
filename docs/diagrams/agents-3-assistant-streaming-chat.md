```mermaid
sequenceDiagram
    autonumber
    actor U as User
    participant FE as sentium-portal
    participant AR as Praxis · AssistantController
    participant TP as AssistantTurnProcessor
    participant SR as StreamRelay
    participant AG as AIAgent (GeneralAssistant)
    participant CH as ConversationChatHistoryProvider

    U->>FE: Send a message
    FE->>AR: POST /api/praxis/assistant/chat
    AR->>SR: Claim(turnId, user)
    AR->>TP: Enqueue the turn
    AR-->>FE: 202 { turnId }
    FE->>AR: GET /api/praxis/assistant/turns/{turnId}/stream (SSE)
    AR->>SR: Subscribe(turnId, after last id)
    TP->>TP: New scope acting as the user, set the run context
    opt Prompt enhancement enabled
        TP->>SR: "enhanced_prompt"
    end
    TP->>AG: RunStreamingAsync(new user turn, session)
    AG->>CH: Load the persisted transcript
    loop Stream the response
        AG-->>SR: reasoning -> "thought", tool call -> "tool_call", text -> "message"
        SR-->>FE: each frame with its sequence number as the SSE id
    end
    AG->>CH: Store the user turn and one merged assistant message
    TP->>SR: terminal "done" (or "cancelled" / "error")
    SR-->>FE: terminal frame, stream ends
    Note over FE,SR: A dropped connection or a reload resumes with Last-Event-ID / ?after= - the turn never noticed
    Note over FE,AR: Stop = POST turns/{turnId}/cancel; an evicted turn's stream answers "gone" and the portal reloads the conversation
    Note over TP,CH: Ephemeral chats (no conversation id) use the client-held history instead
```
