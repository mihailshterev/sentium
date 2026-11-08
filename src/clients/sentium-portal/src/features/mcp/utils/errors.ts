import { ApiError } from "@/shared/api/client";

export function describeError(error: unknown, fallback: string): string {
  if (error instanceof ApiError && error.errors) {
    const messages = Object.values(error.errors).flat();
    if (messages.length > 0) {
      return messages.join(" ");
    }
  }

  return error instanceof Error && error.message ? error.message : fallback;
}
