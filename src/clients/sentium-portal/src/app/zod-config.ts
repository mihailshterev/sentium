import { z } from "zod";

// The Content-Security-Policy forbids eval, so skip Zod's eval-compiled parsers (and its probe for them).
// Imported first in main.tsx: some modules parse with Zod while they load, before main's own code runs.
z.config({ jitless: true });
