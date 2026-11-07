import { describe, it, expect } from "vitest";
import { describeRun } from "./describe-run";

const run = (overrides: Partial<Parameters<typeof describeRun>[0]> = {}) => ({
  title: null,
  triggerType: "workflow.dynamic",
  triggerPayload: JSON.stringify({ activity: "Check the nightly backups" }),
  explanation: "All good",
  ...overrides,
});

describe("describeRun", () => {
  it("prefers the stored title", () => {
    expect(describeRun(run({ title: "Backup audit" })).title).toBe("Backup audit");
  });

  it("falls back to the scenario for untitled runs, without context tags", () => {
    const result = describeRun(
      run({ triggerPayload: JSON.stringify({ activity: "[Workspace: Ops | ID: 1] Check backups" }) }),
    );
    expect(result.title).toBe("Check backups");
  });

  it("shortens long scenarios on a word boundary", () => {
    const activity =
      "Review every deployment pipeline across all services and summarize the risky configuration changes";
    const { title } = describeRun(run({ triggerPayload: JSON.stringify({ activity }) }));
    expect(title.endsWith("…")).toBe(true);
    expect(title.length).toBeLessThanOrEqual(61);
    expect(activity.startsWith(title.slice(0, -1))).toBe(true);
  });

  it("uses a placeholder when there is nothing to show", () => {
    expect(describeRun(run({ triggerPayload: "not json" })).title).toBe("Untitled run");
    expect(describeRun(run({ triggerPayload: "[1,2]" })).title).toBe("Untitled run");
  });

  it("names the custom workflow, or Dynamic", () => {
    expect(describeRun(run()).source).toBe("Dynamic");
    expect(
      describeRun(
        run({
          triggerType: "workflow.custom.workflow",
          triggerPayload: JSON.stringify({ activity: "x", workflowName: "Nightly QA" }),
        }),
      ).source,
    ).toBe("Nightly QA");
    expect(describeRun(run({ triggerType: "workflow.custom.workflow", triggerPayload: "{}" })).source).toBe("workflow");
  });

  it.each([
    ["[FAILED] boom", "failed"],
    ["[CANCELLED] Stopped by user.", "cancelled"],
    ["[TIMEOUT] Workflow exceeded 120 minute(s).", "timeout"],
    ["Finished fine", "completed"],
  ])("derives the outcome from %s", (explanation, outcome) => {
    expect(describeRun(run({ explanation })).outcome).toBe(outcome);
  });
});
