export type McpTransport = "Http" | "SandboxStdio";

export interface McpConnectionState {
  connected: boolean;
  connectedAt: string | null;
  lastUsedAt: string | null;
  lastError: string | null;
}

export interface McpTool {
  name: string;
  qualifiedName: string;
  title: string | null;
  description: string | null;
  approvedDescription: string | null;
  inputSchema: string;
  readOnlyHint: boolean | null;
  destructiveHint: boolean | null;
  idempotentHint: boolean | null;
  openWorldHint: boolean | null;
  enabled: boolean;
  requiresApproval: boolean;
  approved: boolean;
  changed: boolean;
  missing: boolean;
  usable: boolean;
}

export interface McpServer {
  id: string;
  name: string;
  displayName: string;
  description: string | null;
  transport: McpTransport;
  endpoint: string | null;
  image: string | null;
  args: string[];
  network: string | null;
  memoryMb: number | null;
  cpuLimit: number | null;
  secretNames: string[];
  variables: Record<string, string>;
  enabled: boolean;
  tools: McpTool[];
  lastSyncedAt: string | null;
  lastError: string | null;
  connection: McpConnectionState;
  createdAt: string;
  updatedAt: string;
}

export interface CreateMcpServerPayload {
  name: string;
  displayName: string;
  description: string | null;
  transport: McpTransport;
  endpoint: string | null;
  image: string | null;
  args: string[] | null;
  network: string | null;
  memoryMb: number | null;
  cpuLimit: number | null;
  secrets: Record<string, string> | null;
  variables: Record<string, string> | null;
  enabled: boolean;
}

export interface UpdateMcpServerPayload {
  displayName: string;
  description: string | null;
  endpoint: string | null;
  image: string | null;
  args: string[] | null;
  network: string | null;
  memoryMb: number | null;
  cpuLimit: number | null;
  secrets: Record<string, string | null> | null;
  variables: Record<string, string | null> | null;
  enabled: boolean;
}

export interface McpToolUpdate {
  name: string;
  enabled?: boolean;
  approve?: boolean;
  requiresApproval?: boolean;
}

export interface SandboxMcpServerStatus {
  name: string;
  running: boolean;
  containerId: string | null;
  startedAt: string | null;
  lastUsedAt: string | null;
  lastError: string | null;
  stderrTail: string[];
}

export interface McpServerStatus {
  connection: McpConnectionState;
  sandbox: SandboxMcpServerStatus | null;
  sandboxError: string | null;
}
