// Everything a run can be tuned with, read from k6's -e flags (run.sh passes them through).

const number = (name, fallback) => {
  const value = Number(__ENV[name]);
  return __ENV[name] !== undefined && __ENV[name] !== "" && Number.isFinite(value) ? value : fallback;
};

export const BASE_URL = (__ENV.BASE_URL || "http://sentium-web:8080").replace(/\/+$/, "");
export const PROFILE = __ENV.PROFILE || "smoke";
export const RESULTS_DIR = __ENV.RESULTS_DIR || "results";

/** The Testing environment's default model; the load stub declares it as loaded. */
export const MODEL = __ENV.MODEL || "gemma-4-e4b";

/** The Sovereign seeded in the Testing environment (E2E:* in appsettings.Testing.json). */
export const SOVEREIGN = {
  userName: __ENV.SOVEREIGN_USER || "testuser",
  password: __ENV.SOVEREIGN_PASSWORD || "testuser1a",
};

export const LOAD_USER_PREFIX = __ENV.LOAD_USER_PREFIX || "loaduser";
export const LOAD_USER_PASSWORD = __ENV.LOAD_USER_PASSWORD || "loaduser1a";

/** Peak concurrent VUs (or, for breakpoint, peak iterations per second); 0 keeps the profile's default. */
export const PEAK = number("VUS", 0);

/** Length of the profile's main plateau (k6 duration string); empty keeps the profile's default. */
export const HOLD = __ENV.HOLD || "";

/** Turns off think time, e.g. to push a constant-VU profile harder. Arrival-rate profiles never think. */
export const NO_THINK = __ENV.NO_THINK === "1";

/** Must match the stub's settings (compose.load.yaml), so the chat SLO tracks the simulated generation time. */
export const STUB = {
  ttftMs: number("STUB_TTFT_MS", 300),
  tokens: number("STUB_TOKENS", 60),
  tokensPerSec: number("STUB_TOKENS_PER_SEC", 40),
};

/** What Sentium may add on top of the simulated generation for one assistant turn (queueing, history, persistence). */
export const CHAT_OVERHEAD_MS = number("CHAT_OVERHEAD_MS", 1500);

/** The source tag of the knowledge-base documents setup() ingests and teardown() removes. */
export const KB_SOURCE = "load-test-corpus";
export const KB_DOCUMENTS = number("KB_DOCUMENTS", 60);

/** Assistant turns per conversation before a chat VU starts a new one. */
export const TURNS_PER_CONVERSATION = number("TURNS_PER_CONVERSATION", 4);
