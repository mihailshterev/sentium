All modules share one PostgreSQL database (`sentiumdb`); each module owns one schema with its own
migrations history. Relationships never cross a schema: modules refer to each other's rows by id only.

# Agent Core & Skills Domain (`praxis` schema)

```mermaid
erDiagram

Agents {
    guid Id PK
    guid UserId
    string Name
    string Description
    string Model
    datetime CreatedAt
    datetime UpdatedAt
}

AgentSkills {
    guid Id PK
    guid UserId
    string Name
    string Description
    string Instructions
    int SkillType
    string FileName
    datetime CreatedAt
    datetime UpdatedAt
}

AgentLearnings {
    guid Id PK
    guid UserId
    bool IsGlobal
    string AgentName
    string Content
    string Tags
    guid ConversationId
    datetime CapturedAt
    bool IsIngested
}
```

# Conversations Domain (`praxis` schema)

```mermaid
erDiagram

Conversations {
    guid Id PK
    guid UserId
    string Title
    string Model
    datetime CreatedAt
}

Messages {
    guid Id PK
    guid ConversationId FK
    string Role
    string Content
    string EnhancedPrompt
    string Thought
    string ToolCalls
    datetime Timestamp
}

Conversations ||--|{ Messages : contains
```

# Workflow Domain (`praxis` schema)

```mermaid
erDiagram

Workflows {
    guid Id PK
    guid UserId
    string Name
    string Description
    datetime CreatedAt
    datetime UpdatedAt
}

WorkflowAgents {
    guid WorkflowId PK, FK
    guid AgentId PK, FK
    int Order
}

WorkflowRuns {
    guid Id PK
    guid UserId
    guid WorkflowId FK
    string TriggerType
    string TriggerPayload
    string Explanation
    string Risk
    string Recommendation
    datetime StartedAt
    datetime CompletedAt
    string LogJson
}

Workflows ||--|{ WorkflowAgents : contains
Agents ||--|{ WorkflowAgents : participates
Workflows ||--o{ WorkflowRuns : executes
```

# Workspace Domain (`praxis` schema)

```mermaid
erDiagram
direction LR

Workspaces {
    guid Id PK
    guid UserId
    string Name
    string Description
    datetime CreatedAt
    datetime UpdatedAt
}

ProjectFiles {
    guid Id PK
    guid UserId
    guid WorkspaceId FK
    string FileName
    guid BlobName
    string Extension
    long SizeBytes
    int ProcessingStatus
    datetime CreatedAt
}

Workspaces ||--o{ ProjectFiles : contains
```

# Identity Domain (`identity` schema)

```mermaid
erDiagram

Users {
    guid Id PK
    string FirstName
    string LastName
    string UserName
    string NormalizedUserName
    string Email
    string NormalizedEmail
    bool EmailConfirmed
    string PasswordHash
    string SecurityStamp
    string ConcurrencyStamp
    bool TwoFactorEnabled
    int AccessFailedCount
}

Roles {
    guid Id PK
    string Name
    string NormalizedName
    string ConcurrencyStamp
}

UserRoles {
    guid UserId PK, FK
    guid RoleId PK, FK
}

UserClaims {
    int Id PK
    guid UserId FK
    string ClaimType
    string ClaimValue
}

UserTokens {
    guid UserId PK, FK
    string LoginProvider PK
    string Name PK
    string Value
}

Users ||--|{ UserRoles : has
Roles ||--|{ UserRoles : assigns

Users ||--|{ UserClaims : defines
Users ||--|{ UserTokens : owns
```

# Knowledge Domain (`codex` schema)

```mermaid
erDiagram

knowledge_chunks {
    guid Id PK
    string Collection
    string Content
    string Source
    string SourceType
    string Scope
    guid UserId
    jsonb Metadata
    datetimeoffset CreatedAt
    vector Embedding "vector(768), HNSW cosine index"
}
```

# Audit Domain (`sentinel` schema)

```mermaid
erDiagram

AuditLogs {
    guid Id PK
    datetimeoffset Timestamp
    string CorrelationId
    string AgentId
    string SkillName
    string Action
    string ResourceType
    string ResourceId
    string UserPromptHash
    bool Allowed
    string Effect
    string Risk
    string Reason
    bigint EvaluationDurationMs
    string AlignmentVerdict
    string MetadataJson
    string TriggeredPoliciesJson
}
```

# Settings Domain (`registry` schema)

```mermaid
erDiagram

SystemSettings {
    guid Id PK
    guid UserId
    string Settings
    datetimeoffset UpdatedAt
    string UpdatedBy
}
```

# Notifications Domain (`nuntius` schema)

```mermaid
erDiagram

Notifications {
    guid Id PK
    guid UserId
    string Category
    string Severity
    string Title
    string Body
    string Link
    string DedupeKey
    int Occurrences
    datetimeoffset CreatedAt
    datetimeoffset UpdatedAt
    datetimeoffset ReadAt
}
```

`UserId` references an Identity user by value (no cross-schema foreign key). A partial unique index on
`(UserId, DedupeKey) WHERE ReadAt IS NULL AND DedupeKey IS NOT NULL` keeps at most one unread Notification per key.

# Execution Domain (`praxis` schema)

```mermaid
erDiagram

ExecutionLogs {
    guid JobId PK
    datetimeoffset ExecutedAt
    string AgentId
    string CorrelationId
    guid SentinelAuditId
    string Language
    string Code
    string OriginalUserPrompt
    bool Succeeded
    long ExitCode
    string Output
    string Error
    bool TimedOut
    long DurationMs
    bool PolicyDenied
    string PolicyDenialReason
    string FileContext
    string Artifacts
}
```
