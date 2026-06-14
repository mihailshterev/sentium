# Agent Core & Skills Domain

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

# Conversations Domain

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

# Workflow Domain

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

# Workspace Domain

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

## Identity Domain

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

## OpenIddict Domain

```mermaid
erDiagram
direction LR

OpenIddictApplications {
    string Id PK
    string ClientId
    string ClientSecret
    string Type
    string ConsentType
    string DisplayName
    string Permissions
    string RedirectUris
    string PostLogoutRedirectUris
    string Requirements
}

OpenIddictAuthorizations {
    string Id PK
    string ApplicationId FK
    string Subject
    string Status
    string Type
    string Scopes
    datetime CreationDate
}

OpenIddictTokens {
    string Id PK
    string ApplicationId FK
    string AuthorizationId FK
    string Subject
    string Type
    string Status
    string Payload
    datetime CreationDate
    datetime ExpirationDate
    datetime RedemptionDate
}

OpenIddictApplications ||--|{ OpenIddictAuthorizations : grants
OpenIddictApplications ||--|{ OpenIddictTokens : issues

OpenIddictAuthorizations ||--|{ OpenIddictTokens : backs
```

# Audit Domain

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

# Settings Domain

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

# Execution Domain

```mermaid
erDiagram

ExecutionLogs {
    guid JobId PK
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
