/** A personal fact an agent saved for the signed-in user. Private to them, Sovereigns included. */
export interface UserMemory {
  id: string;
  content: string;
  createdAt: string;
  updatedAt: string;
}

/** Mirrors the server's limit. */
export const MAX_MEMORY_LENGTH = 2000;
