// Entry point of the load suite: `k6 run -e PROFILE=<smoke|load|stress|spike|soak|breakpoint> main.js`.
// See load/README.md for the workload model, the SLOs and how to read the results.
import { MODEL, PROFILE, RESULTS_DIR } from "./lib/config.js";
import { seed, unseed } from "./lib/seed.js";
import { scenarios, userCount } from "./profiles.js";
import { generationMs, thresholds } from "./slo.js";

export { browse } from "./scenarios/browse.js";
export { write } from "./scenarios/write.js";
export { chat } from "./scenarios/chat.js";
export { search } from "./scenarios/search.js";
export { auth } from "./scenarios/auth.js";

export const options = {
  scenarios,
  thresholds,
  // A VU is a member who stays signed in: keep its session cookie across iterations.
  noCookiesReset: true,
  setupTimeout: "10m",
  teardownTimeout: "2m",
  summaryTrendStats: ["avg", "min", "med", "max", "p(90)", "p(95)", "p(99)"],
  userAgent: "sentium-load/1.0 (k6)",
  tags: { profile: PROFILE },
};

export function setup() {
  return { ...seed(userCount), model: MODEL };
}

export function teardown(data) {
  unseed(data);
}

const pad = (value, width) => String(value).padEnd(width);
const ms = (value) => (value === undefined ? "-" : `${value.toFixed(1)} ms`);

/** A compact, dependency-free console summary (jslib's textSummary would be a runtime download). */
function textSummary(data) {
  const lines = [
    `\nSentium load test - profile ${PROFILE} (simulated generation ${Math.round(generationMs)} ms per answer)\n`,
  ];

  lines.push(`${pad("metric", 46)}${pad("p95", 14)}${pad("p99", 14)}${pad("count/rate", 14)}result`);
  for (const [name, metric] of Object.entries(data.metrics)) {
    if (!metric.thresholds) {
      continue;
    }
    const failed = Object.values(metric.thresholds).some((t) => !t.ok);
    const values = metric.values;
    const volume =
      values.count !== undefined ? values.count : values.rate !== undefined ? `${(values.rate * 100).toFixed(2)}%` : "";
    lines.push(
      `${pad(name, 46)}${pad(ms(values["p(95)"]), 14)}${pad(ms(values["p(99)"]), 14)}${pad(volume, 14)}${failed ? "FAIL" : "ok"}`,
    );
  }

  const iterations = data.metrics.iterations ? data.metrics.iterations.values : {};
  const requests = data.metrics.http_reqs ? data.metrics.http_reqs.values : {};
  lines.push(
    `\nrequests ${requests.count ?? 0} (${(requests.rate ?? 0).toFixed(1)}/s), iterations ${iterations.count ?? 0}`,
  );
  return `${lines.join("\n")}\n`;
}

export function handleSummary(data) {
  return {
    stdout: textSummary(data),
    [`${RESULTS_DIR}/${PROFILE}-summary.json`]: JSON.stringify(data, null, 2),
  };
}
