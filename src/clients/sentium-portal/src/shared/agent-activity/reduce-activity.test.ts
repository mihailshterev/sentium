import { describe, expect, it } from "vitest";
import {
  appendContent,
  appendThought,
  decideApproval,
  emptyTurn,
  finalizeTurn,
  finishTool,
  requestApproval,
  startTool,
  stepsFromDto,
  stepsFromWorkflowLog,
} from "./reduce-activity";
import type { ActivityTurn, ThoughtStep } from "./types";

const kinds = (turn: ActivityTurn) => turn.steps.map((s) => s.kind);

describe("assistant turn reducer", () => {
  it("keeps thought → tool → thought as three ordered steps", () => {
    let turn = appendThought(emptyTurn(), "Let me ", 0);
    turn = appendThought(turn, "search.", 10);
    turn = startTool(turn, { callId: "c1", name: "knowledge_base_search", arguments: '{"query":"x"}' }, 20);
    turn = finishTool(turn, { callId: "c1", status: "ok", result: "3 hits", durationMs: 90 });
    turn = appendThought(turn, "Enough.", 200);
    turn = appendContent(turn, "Answer", 300);
    turn = finalizeTurn(turn, 400);

    expect(kinds(turn)).toEqual(["thought", "tool", "thought"]);
    expect(turn.steps[0]).toMatchObject({ text: "Let me search.", startedAt: 0, endedAt: 20 });
    expect(turn.steps[1]).toMatchObject({ status: "ok", result: "3 hits", durationMs: 90 });
    expect((turn.steps[2] as ThoughtStep).endedAt).toBe(300);
    expect(turn.content).toBe("Answer");
  });

  it("moves narration before a tool call onto the timeline", () => {
    let turn = appendContent(emptyTurn(), "Checking the inventory.");
    turn = startTool(turn, { callId: "c1", name: "find_assets" });
    turn = appendContent(turn, "Found it.");

    expect(kinds(turn)).toEqual(["text", "tool"]);
    expect(turn.steps[0]).toMatchObject({ text: "Checking the inventory." });
    expect(turn.content).toBe("Found it.");
  });

  it("promotes the last narration to the answer when nothing follows the tools", () => {
    let turn = appendContent(emptyTurn(), "Saved your note.");
    turn = startTool(turn, { callId: "c1", name: "add_timeline_note" });
    turn = finishTool(turn, { callId: "c1", status: "ok" });
    turn = appendThought(turn, "done");
    turn = appendContent(turn, "");

    expect(finalizeTurn(turn).content).toBe("");

    const narratedLast = finalizeTurn({
      content: "",
      steps: [...turn.steps, { kind: "text", id: "x", text: "All set." }],
    });
    expect(narratedLast.content).toBe("All set.");
    expect(narratedLast.steps.at(-1)?.kind).not.toBe("text");
  });

  it("ignores a repeated call and keeps a policy denial", () => {
    let turn = startTool(emptyTurn(), { callId: "c1", name: "write_workspace_file" });
    turn = startTool(turn, { callId: "c1", name: "write_workspace_file" });
    turn = finishTool(turn, { callId: "c1", status: "denied", result: "egress blocked" });

    expect(turn.steps).toHaveLength(1);
    expect(turn.steps[0]).toMatchObject({ status: "denied", result: "egress blocked" });
  });

  it("tracks an approval from request to decision to result", () => {
    let turn = requestApproval(emptyTurn(), {
      requestId: "r1",
      toolName: "execute_code_sandbox",
      arguments: { input: "print(1)" },
      callId: "c9",
    });
    expect(turn.steps[0]).toMatchObject({ status: "awaiting_approval", arguments: '{"input":"print(1)"}' });

    turn = decideApproval(turn, { callId: "c9", toolName: "execute_code_sandbox" }, true, 50);
    expect(turn.steps[0]).toMatchObject({ status: "running", startedAt: 50 });

    turn = startTool(turn, { callId: "c9", name: "execute_code_sandbox" });
    turn = finishTool(turn, { callId: "c9", status: "ok", result: "1" });
    expect(turn.steps).toHaveLength(1);
    expect(turn.steps[0]).toMatchObject({ status: "ok", result: "1" });
  });

  it("keeps a rejected call rejected even when the model gets a result back", () => {
    let turn = requestApproval(emptyTurn(), { requestId: "r1", toolName: "schedule_recurring_task", callId: "c2" });
    turn = decideApproval(turn, { callId: "c2", toolName: "schedule_recurring_task" }, false);
    turn = finishTool(turn, { callId: "c2", status: "ok", result: "rejected by user" });

    expect(turn.steps[0]).toMatchObject({ status: "rejected" });
  });
});

describe("stepsFromDto", () => {
  it("maps persisted ordered steps", () => {
    const steps = stepsFromDto("m1", {
      steps: [
        { kind: "thought", text: "hmm" },
        { kind: "tool", callId: "c1", name: "find_assets", status: "error", result: "boom", durationMs: 4 },
        { kind: "text", text: "Retrying." },
      ],
    });
    expect(steps.map((s) => s.kind)).toEqual(["thought", "tool", "text"]);
    expect(steps[1]).toMatchObject({ id: "c1", status: "error", durationMs: 4 });
  });

  it("rebuilds legacy messages and skips empty reasoning", () => {
    expect(stepsFromDto("m1", { thought: null, toolCalls: null })).toEqual([]);
    expect(stepsFromDto("m1", { thought: "", toolCalls: [] })).toEqual([]);

    const legacy = stepsFromDto("m1", { thought: "old reasoning", toolCalls: ["Calling read_file..."] });
    expect(legacy).toMatchObject([
      { kind: "thought", text: "old reasoning" },
      { kind: "tool", name: "read_file", status: "ok" },
    ]);
  });
});

describe("stepsFromWorkflowLog", () => {
  it("merges per-agent deltas, pairs results with calls and renders legacy labels", () => {
    const steps = stepsFromWorkflowLog([
      { author: "System", text: "Step 1/2 - ResearchAnalyst: look", type: "status" },
      { author: "ResearchAnalyst", text: "Think", type: "thought" },
      { author: "ResearchAnalyst", text: "ing", type: "thought" },
      {
        author: "ResearchAnalyst",
        text: JSON.stringify({ callId: "c1", name: "knowledge_base_search" }),
        type: "tool_call",
      },
      {
        author: "ResearchAnalyst",
        text: JSON.stringify({ callId: "c1", status: "ok", result: "hits", durationMs: 30 }),
        type: "tool_result",
      },
      { author: "ResearchAnalyst", text: "Calling read_file...", type: "tool" },
      { author: "ResearchAnalyst", text: "Brief ", type: "message" },
      { author: "ResearchAnalyst", text: "done.", type: "message" },
      { author: "Validator", text: "Step 1 passed review", type: "status" },
      { author: "System", text: "boom", type: "error" },
    ]);

    expect(steps.map((s) => s.kind)).toEqual(["status", "thought", "tool", "tool", "output", "status", "status"]);
    expect(steps[1]).toMatchObject({ text: "Thinking", author: "ResearchAnalyst" });
    expect(steps[2]).toMatchObject({ name: "knowledge_base_search", status: "ok", durationMs: 30 });
    expect(steps[3]).toMatchObject({ name: "read_file", status: "ok" });
    expect(steps[4]).toMatchObject({ text: "Brief done." });
    expect(steps[5]).toMatchObject({ tone: "pass" });
    expect(steps[6]).toMatchObject({ tone: "fail" });
  });
});
