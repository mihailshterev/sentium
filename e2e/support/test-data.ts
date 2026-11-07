import { randomUUID } from "node:crypto";
import { test } from "@playwright/test";

/**
 * A name no other test, worker, browser project or earlier run (against a reused server) can produce.
 * Every test shares the one seeded user, so anything a test creates must be uniquely named.
 */
export function uniqueName(prefix: string, maxLength = 64): string {
  const project = test.info().project.name;
  return `${prefix}-${project}-${randomUUID().slice(0, 8)}`.slice(0, maxLength);
}
