#!/usr/bin/env node
// Compares a k6 summary (handleSummary JSON) with a committed baseline and fails on latency regressions.
//
//   node compare.mjs <summary.json> <baseline.json> [--tolerance 15] [--markdown out.md]
//   node compare.mjs <summary.json> <baseline.json> --update      # writes the summary as the new baseline
//
// A baseline keeps only the tracked statistics of the latency metrics that carry thresholds. Without a baseline the
// comparison passes with a notice, so a new profile doesn't fail until someone accepts its first run.
import { existsSync, readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname } from "node:path";

const STATS = ["p(95)", "p(99)"];
/** Below this, differences are noise (timer resolution, scheduling), whatever the percentage. */
const MIN_DELTA_MS = 5;
const BREAKDOWN_ONLY = "max>=0";

const args = process.argv.slice(2);
const flag = (name, fallback) => {
  const index = args.indexOf(`--${name}`);
  return index >= 0 ? args[index + 1] : fallback;
};
const [summaryPath, baselinePath] = args.filter((arg, i) => !arg.startsWith("--") && !args[i - 1]?.startsWith("--"));
const tolerance = Number(flag("tolerance", "15"));
const markdownPath = flag("markdown", "");
const update = args.includes("--update");

if (!summaryPath || !baselinePath) {
  console.error("usage: compare.mjs <summary.json> <baseline.json> [--tolerance 15] [--markdown out.md] [--update]");
  process.exit(2);
}

/** The latency metrics worth tracking: trends (ms) that the SLOs put a threshold on. */
function slim(summary) {
  const metrics = {};
  for (const [name, metric] of Object.entries(summary.metrics ?? {})) {
    const values = metric.values ?? {};
    if (metric.type !== "trend" || !isObjective(metric) || values["p(95)"] === undefined || values.count === 0) {
      continue;
    }
    metrics[name] = Object.fromEntries(
      STATS.filter((stat) => values[stat] !== undefined).map((stat) => [stat, round(values[stat])]),
    );
  }
  return metrics;
}

/** slo.js gives breakdown-only metrics (setup, raw chat requests) a "max>=0" placeholder; those are no objective. */
const isObjective = (metric) => Object.keys(metric.thresholds ?? {}).some((threshold) => threshold !== BREAKDOWN_ONLY);

const round = (value) => Math.round(value * 100) / 100;

const summary = JSON.parse(readFileSync(summaryPath, "utf8"));
const current = slim(summary);

if (update) {
  mkdirSync(dirname(baselinePath), { recursive: true });
  const baseline = {
    createdAt: new Date().toISOString(),
    note: "Accepted k6 run; regenerate with compare.mjs --update.",
    metrics: current,
  };
  writeFileSync(baselinePath, `${JSON.stringify(baseline, null, 2)}\n`);
  console.log(`Baseline written to ${baselinePath} (${Object.keys(current).length} metrics).`);
  process.exit(0);
}

const output = [];
let regressions = 0;

if (!existsSync(baselinePath)) {
  output.push(`No baseline at \`${baselinePath}\` yet: nothing to compare. Accept a run with \`--update\`.`);
} else {
  const baseline = JSON.parse(readFileSync(baselinePath, "utf8")).metrics ?? {};
  output.push(
    `Latency vs baseline (tolerance ${tolerance}%)`,
    "",
    "| metric | stat | baseline | current | change | |",
    "|---|---|---:|---:|---:|---|",
  );

  for (const [name, stats] of Object.entries(baseline)) {
    for (const [stat, before] of Object.entries(stats)) {
      const after = current[name]?.[stat];
      if (after === undefined) {
        output.push(`| ${name} | ${stat} | ${before} | - | - | missing |`);
        continue;
      }
      const change = before > 0 ? ((after - before) / before) * 100 : 0;
      const regressed = change > tolerance && after - before > MIN_DELTA_MS;
      regressions += regressed ? 1 : 0;
      const verdict = regressed ? "REGRESSION" : change < -tolerance ? "faster" : "ok";
      output.push(
        `| ${name} | ${stat} | ${before} ms | ${after} ms | ${change >= 0 ? "+" : ""}${change.toFixed(1)}% | ${verdict} |`,
      );
    }
  }
  output.push("", regressions ? `${regressions} regression(s) beyond ${tolerance}%.` : "No regressions.");
}

const text = output.join("\n");
console.log(text);
if (markdownPath) {
  writeFileSync(markdownPath, `${text}\n`);
}
process.exit(regressions ? 1 : 0);
