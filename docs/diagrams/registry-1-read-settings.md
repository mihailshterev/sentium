```mermaid
sequenceDiagram
    autonumber
    participant M as Consuming module (Praxis / Sentinel / Vigil)
    participant SS as Registry · ISettingsService (ScopedSettingsService)
    participant SV as SettingsService
    participant HC as HybridCache (L1 + Redis L2)
    participant DB as PostgreSQL (registry schema)

    M->>SS: GetValueAsync(key, userId) via Registry Contracts
    SS->>SV: GetAsync(key, scope) in its own DI scope
    SV->>HC: GetOrCreate(settings:envelope:{key}:{scope})
    alt Cache hit
        HC-->>SV: SettingsEnvelope
    else Cache miss
        HC->>DB: Load the settings row (JSON column)
        DB-->>HC: row, or defaults when none exists
        HC-->>SV: SettingsEnvelope (stored in L1 + L2)
    end
    SV-->>M: envelope.Value deserialized into the module's own record
    Note over M,SS: Modules keep no copy. If Registry cannot be read they fall back to built-in defaults.
```
