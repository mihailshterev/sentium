// The service-level objectives every profile is judged against. Keep load/README.md's SLO table in step.
//
// Thresholds select requests by the `type` tag set in lib/http.js. Setup traffic (type:setup) and the long-lived turn
// streams (type:stream, measured as chat_turn_ms instead) are left out of the latency objectives.
import { CHAT_OVERHEAD_MS, STUB } from "./lib/config.js";
import { isArrivalRate, profileName } from "./profiles.js";

/** The simulated generation time of one answer: time to first token plus streaming the tokens. */
export const generationMs = STUB.ttftMs + (STUB.tokens / STUB.tokensPerSec) * 1000;

// Only the breakpoint test stops at the first sustained breach - finding that point is its purpose. Stress and spike
// keep running past a breach so the report shows how far and how long the system degrades.
const abort = isArrivalRate;

const objective = (threshold) => (abort ? { threshold, abortOnFail: true, delayAbortEval: "1m" } : threshold);

export const thresholds = {
  http_req_failed: [objective(profileName === "smoke" ? "rate==0" : "rate<0.01")],
  checks: [objective("rate>0.99")],

  "http_req_duration{type:read}": [objective("p(95)<300"), objective("p(99)<800")],
  "http_req_duration{type:write}": [objective("p(95)<500"), objective("p(99)<1000")],
  "http_req_duration{type:search}": [objective("p(95)<800"), objective("p(99)<1500")],
  "http_req_duration{type:auth}": [objective("p(95)<800")],

  chat_accept_ms: [objective("p(95)<300")],
  chat_turn_ms: [objective(`p(95)<${Math.round(generationMs + CHAT_OVERHEAD_MS)}`)],
  chat_turn_completed: [objective("rate>0.99")],

  // Not objectives, just there so the summary breaks these out per type.
  "http_req_duration{type:chat}": ["max>=0"],
  "http_req_duration{type:setup}": ["max>=0"],
};
