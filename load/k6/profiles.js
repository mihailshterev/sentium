// The standard test types as k6 scenario sets. Each profile describes one load shape for the whole household; it is
// split across the scenarios by WEIGHTS, so every profile runs the same workload mix.
import { HOLD, PEAK, PROFILE } from "./lib/config.js";

/** Share of the concurrent users (or of the arrival rate) each scenario gets. */
export const WEIGHTS = { browse: 0.6, write: 0.15, chat: 0.15, search: 0.07, auth: 0.03 };

const SCENARIO_EXEC = { browse: "browse", write: "write", chat: "chat", search: "search", auth: "auth" };

const PROFILES = {
  // Script and SLO sanity: one VU per scenario.
  smoke: { kind: "constant", peak: 5, hold: "1m" },
  // The expected peak, with headroom: a household plus its scheduled agents rarely exceeds a handful of active users.
  load: {
    kind: "ramp",
    peak: 20,
    hold: "20m",
    stages: (p, h) => [
      ["5m", p],
      [h, p],
      ["2m", 0],
    ],
  },
  // Steps past the expected peak to find where the SLOs start to break.
  stress: {
    kind: "ramp",
    peak: 150,
    hold: "5m",
    stages: (p, h) => [
      ["2m", p * 0.13],
      [h, p * 0.13],
      ["2m", p / 3],
      [h, p / 3],
      ["2m", p * 0.67],
      [h, p * 0.67],
      ["2m", p],
      [h, p],
      ["3m", 0],
    ],
  },
  // A sudden burst, then a quiet stretch to check the system recovers on its own.
  spike: {
    kind: "ramp",
    peak: 150,
    hold: "2m",
    stages: (p, h) => [
      ["1m", 5],
      ["30s", p],
      [h, p],
      ["30s", 5],
      ["5m", 5],
      ["30s", 0],
    ],
  },
  // Moderate load for a long time: memory, connection, cache and SSE relay leaks show up here.
  soak: {
    kind: "ramp",
    peak: 15,
    hold: "2h",
    stages: (p, h) => [
      ["5m", p],
      [h, p],
      ["2m", 0],
    ],
  },
  // Open-model ramp of iterations per second until thresholds abort the run: the maximum sustainable throughput.
  breakpoint: { kind: "arrival", peak: 200, hold: "30m" },
};

const profile = PROFILES[PROFILE];
if (!profile) {
  throw new Error(`Unknown PROFILE "${PROFILE}". Use one of: ${Object.keys(PROFILES).join(", ")}`);
}

export const isArrivalRate = profile.kind === "arrival";

const peak = PEAK > 0 ? PEAK : profile.peak;
const hold = HOLD || profile.hold;

const share = (value, weight) => (value <= 0 ? 0 : Math.max(1, Math.round(value * weight)));

function scenario(name, weight) {
  const base = { exec: SCENARIO_EXEC[name], tags: { scenario_group: name } };

  if (profile.kind === "constant") {
    return { ...base, executor: "constant-vus", vus: share(peak, weight), duration: hold };
  }

  if (profile.kind === "arrival") {
    const rate = share(peak, weight);
    return {
      ...base,
      executor: "ramping-arrival-rate",
      startRate: 1,
      timeUnit: "1s",
      preAllocatedVUs: Math.max(2, rate),
      maxVUs: Math.max(10, rate * 5),
      stages: [{ duration: hold, target: rate }],
    };
  }

  return {
    ...base,
    executor: "ramping-vus",
    startVUs: 0,
    gracefulRampDown: "30s",
    stages: profile.stages(peak, hold).map(([duration, target]) => ({ duration, target: share(target, weight) })),
  };
}

export const scenarios = Object.fromEntries(
  Object.entries(WEIGHTS).map(([name, weight]) => [name, scenario(name, weight)]),
);

function maxVUs(definition) {
  if (definition.vus) {
    return definition.vus;
  }
  if (definition.maxVUs) {
    return definition.maxVUs;
  }
  return Math.max(...definition.stages.map((stage) => stage.target));
}

/** One account per VU that can be alive at once, so concurrent VUs never share a user. */
export const userCount = Object.values(scenarios).reduce((sum, definition) => sum + maxVUs(definition), 0);

export const profileName = PROFILE;
