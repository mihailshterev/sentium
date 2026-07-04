import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { useOrchestrationRunStore } from "./orchestration-run-store";
import * as agentRuntimeService from "../services/agentRuntime.service";

class FakeEventSource {
  static instances: FakeEventSource[] = [];
  url: string;
  onmessage: ((e: { data: string }) => void) | null = null;
  onerror: (() => void) | null = null;
  closed = false;

  constructor(url: string) {
    this.url = url;
    FakeEventSource.instances.push(this);
  }
  close() {
    this.closed = true;
  }
  emit(data: string) {
    this.onmessage?.({ data });
  }
  fail() {
    this.onerror?.();
  }
  static last() {
    return FakeEventSource.instances[FakeEventSource.instances.length - 1];
  }
}

const resetStore = () =>
  useOrchestrationRunStore.setState({
    logs: [],
    phase: "IDLE",
    isRunning: false,
    isDynamicRun: false,
  });

beforeEach(() => {
  FakeEventSource.instances = [];
  vi.stubGlobal("EventSource", FakeEventSource as unknown as typeof EventSource);
  vi.spyOn(agentRuntimeService, "runWorkflowPipeline").mockResolvedValue({ eventId: "e1" });
  vi.spyOn(agentRuntimeService, "runDynamicWorkflow").mockResolvedValue({ eventId: "e2" });
  vi.spyOn(agentRuntimeService, "cancelOrchestrationRun").mockResolvedValue(undefined);
  resetStore();
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe("startPredefined", () => {
  it("begins a non-dynamic run and opens a stream", async () => {
    await useOrchestrationRunStore.getState().startPredefined({ workflowId: "w1", scenario: "s" });

    const state = useOrchestrationRunStore.getState();
    expect(state.isRunning).toBe(true);
    expect(state.isDynamicRun).toBe(false);
    expect(state.phase).toBe("PLANNING");
    expect(FakeEventSource.last().url).toContain("/orchestration/stream/e1");
  });

  it("settles to COMPLETE when the pipeline call throws", async () => {
    vi.spyOn(agentRuntimeService, "runWorkflowPipeline").mockRejectedValueOnce(new Error("boom"));
    await useOrchestrationRunStore.getState().startPredefined({ workflowId: "w1", scenario: "s" });

    const state = useOrchestrationRunStore.getState();
    expect(state.phase).toBe("COMPLETE");
    expect(state.isRunning).toBe(false);
  });
});

describe("startDynamic", () => {
  it("marks the run as dynamic and opens a stream", async () => {
    await useOrchestrationRunStore.getState().startDynamic({ activity: "investigate" });

    const state = useOrchestrationRunStore.getState();
    expect(state.isDynamicRun).toBe(true);
    expect(state.isRunning).toBe(true);
    expect(FakeEventSource.last().url).toContain("/orchestration/stream/e2");
  });
});

describe("stream message handling", () => {
  const startAndGetSource = async () => {
    await useOrchestrationRunStore.getState().startPredefined({ workflowId: "w1", scenario: "s" });
    return FakeEventSource.last();
  };

  it("ignores empty or 'null' payloads", async () => {
    const src = await startAndGetSource();
    src.emit("");
    src.emit("null");
    expect(useOrchestrationRunStore.getState().logs).toHaveLength(0);
  });

  it("ignores malformed JSON", async () => {
    const src = await startAndGetSource();
    src.emit("{ not json");
    expect(useOrchestrationRunStore.getState().logs).toHaveLength(0);
  });

  it("appends a message log and sets PLANNING phase for orchestrator author", async () => {
    const src = await startAndGetSource();
    src.emit(JSON.stringify({ type: "message", author: "Orchestrator", text: "Planning..." }));

    const state = useOrchestrationRunStore.getState();
    expect(state.phase).toBe("PLANNING");
    expect(state.logs).toEqual([{ author: "Orchestrator", text: "Planning...", type: "message" }]);
  });

  it("sets VALIDATING phase for validator author", async () => {
    const src = await startAndGetSource();
    src.emit(JSON.stringify({ type: "message", author: "Validator", text: "checking" }));
    expect(useOrchestrationRunStore.getState().phase).toBe("VALIDATING");
  });

  it("sets SQUAD phase for a generic agent author", async () => {
    const src = await startAndGetSource();
    src.emit(JSON.stringify({ type: "message", author: "Worker", text: "doing" }));
    expect(useOrchestrationRunStore.getState().phase).toBe("SQUAD");
  });

  it("coalesces consecutive same-author message chunks", async () => {
    const src = await startAndGetSource();
    src.emit(JSON.stringify({ type: "message", author: "Worker", text: "Hello " }));
    src.emit(JSON.stringify({ type: "message", author: "Worker", text: "world" }));

    const logs = useOrchestrationRunStore.getState().logs;
    expect(logs).toHaveLength(1);
    expect(logs[0].text).toBe("Hello world");
  });

  it("closes the stream and completes on a 'done' event", async () => {
    const src = await startAndGetSource();
    src.emit(JSON.stringify({ Type: "done" }));

    const state = useOrchestrationRunStore.getState();
    expect(state.phase).toBe("COMPLETE");
    expect(state.isRunning).toBe(false);
    expect(src.closed).toBe(true);
  });

  it("completes after exceeding the reconnect limit on errors", async () => {
    const src = await startAndGetSource();
    for (let i = 0; i < 6; i++) src.fail();

    const state = useOrchestrationRunStore.getState();
    expect(state.phase).toBe("COMPLETE");
    expect(state.isRunning).toBe(false);
  });
});

describe("stopRun / resetRun", () => {
  it("stopRun requests backend cancellation and enters the stopping state", async () => {
    await useOrchestrationRunStore.getState().startPredefined({ workflowId: "w1", scenario: "s" });

    useOrchestrationRunStore.getState().stopRun();

    const state = useOrchestrationRunStore.getState();
    expect(state.connection).toBe("stopping");
    expect(state.isRunning).toBe(true);
    expect(agentRuntimeService.cancelOrchestrationRun).toHaveBeenCalledWith("e1");
  });

  it("stopRun finalizes as stopped when the backend confirms cancellation", async () => {
    await useOrchestrationRunStore.getState().startPredefined({ workflowId: "w1", scenario: "s" });
    const src = FakeEventSource.last();

    useOrchestrationRunStore.getState().stopRun();
    src.emit(JSON.stringify({ Type: "cancelled" }));

    const state = useOrchestrationRunStore.getState();
    expect(state.phase).toBe("COMPLETE");
    expect(state.isRunning).toBe(false);
    expect(state.lastOutcome).toBe("stopped");
    expect(src.closed).toBe(true);
  });

  it("stopRun finalizes as stopped via the fallback timer if the backend never confirms", async () => {
    await useOrchestrationRunStore.getState().startPredefined({ workflowId: "w1", scenario: "s" });
    const src = FakeEventSource.last();

    vi.useFakeTimers();
    try {
      useOrchestrationRunStore.getState().stopRun();
      expect(useOrchestrationRunStore.getState().isRunning).toBe(true);

      await vi.advanceTimersByTimeAsync(8_000);

      const state = useOrchestrationRunStore.getState();
      expect(state.phase).toBe("COMPLETE");
      expect(state.isRunning).toBe(false);
      expect(state.lastOutcome).toBe("stopped");
      expect(src.closed).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });

  it("resetRun is a no-op while a run is in progress", () => {
    useOrchestrationRunStore.setState({ isRunning: true, phase: "SQUAD" });
    useOrchestrationRunStore.getState().resetRun();
    expect(useOrchestrationRunStore.getState().phase).toBe("SQUAD");
  });

  it("resetRun clears state when idle", () => {
    useOrchestrationRunStore.setState({
      isRunning: false,
      phase: "COMPLETE",
      logs: [{ author: "A", text: "t", type: "message" }],
      isDynamicRun: true,
    });
    useOrchestrationRunStore.getState().resetRun();

    const state = useOrchestrationRunStore.getState();
    expect(state.phase).toBe("IDLE");
    expect(state.logs).toHaveLength(0);
    expect(state.isDynamicRun).toBe(false);
  });
});
