export type FileProcessingStatus = "Pending" | "Processing" | "Completed" | "Failed";

export interface WorkspaceFile {
  id: string;
  fileName: string;
  extension: string;
  sizeBytes: number;
  workspaceId: string | null;
  processingStatus: FileProcessingStatus;
  createdAt: string;
}

export interface Workspace {
  id: string;
  name: string;
  description: string | null;
  fileCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreateWorkspacePayload {
  name: string;
  description?: string;
}

export interface UpdateWorkspacePayload {
  name: string;
  description?: string;
}
