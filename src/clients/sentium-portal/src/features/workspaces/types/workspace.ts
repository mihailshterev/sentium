export type FileProcessingStatus = "Pending" | "Processing" | "Completed" | "Failed";

export interface WorkspaceFile {
  id: string;
  fileName: string;
  extension: string;
  sizeBytes: number;
  workspaceId: string | null;
  processingStatus: FileProcessingStatus;
  createdAt: string;
  /** Why the last ingestion attempt failed. */
  processingError?: string | null;
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

/** What the server accepts for upload; the single source of truth for client-side checks. */
export interface UploadRules {
  extensions: string[];
  maxFileSizeBytes: number;
}

/** A file's extracted text for previewing, cut at the requested length when `truncated`. */
export interface WorkspaceFileText {
  fileId: string;
  fileName: string;
  text: string;
  truncated: boolean;
  totalChars: number;
}
