import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { useOrchestrationRunStore } from "./orchestration-run-store";
import * as orchestrationService from "@/features/orchestration/api/orchestration.service";
import { FakeEventSource } from "@/test/fake-event-source";

/** Frames are applied in batches; wait for the batch holding the ones just emitted. */
const applied = () => new Promise((resolve) => setTimeout(resolve, 60));

const resetStore = () =>
  useOrchestrationRunStore.setState({
    logs: [],
    phase: "IDLE",
    isRunning: false,
    isDynamicRun: false,
  });

beforeEach(() => {
  sessionStorage.clear();
  FakeEventSource.install();
  vi.spyOn(orchestrationService, "runWorkflowPipeline").mockResolvedValue({ eventId: "e1" });
  vi.spyOn(orchestrationService, "runDynamicWorkflow").mockResolvedValue({ eventId: "e2" });
  vi.spyOn(orchestrationService, "cancelOrchestrationRun").mockResolvedValue(undefined);
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
    vi.spyOn(orchestrationService, "runWorkflowPipeline").mockRejectedValueOnce(new Error("boom"));
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
    await applied();

    const state = useOrchestrationRunStore.getState();
    expect(state.phase).toBe("PLANNING");
    expect(state.logs).toEqual([{ author: "Orchestrator", text: "Planning...", type: "message" }]);
  });

  it("sets VALIDATING phase for validator author", async () => {
    const src = await startAndGetSource();
    src.emit(JSON.stringify({ type: "message", author: "Validator", text: "checking" }));
    await applied();
    expect(useOrchestrationRunStore.getState().phase).toBe("VALIDATING");
  });

  it("sets SQUAD phase for a generic agent author", async () => {
    const src = await startAndGetSource();
    src.emit(JSON.stringify({ type: "message", author: "Worker", text: "doing" }));
    await applied();
    expect(useOrchestrationRunStore.getState().phase).toBe("SQUAD");
  });

  it("coalesces consecutive same-author message chunks", async () => {
    const src = await startAndGetSource();
    src.emit(JSON.stringify({ type: "message", author: "Worker", text: "Hello " }));
    src.emit(JSON.stringify({ type: "message", author: "Worker", text: "world" }));
    await applied();

    const logs = useOrchestrationRunStore.getState().logs;
    expect(logs).toHaveLength(1);
    expect(logs[0].text).toBe("Hello world");
  });

  it("closes the stream and completes on a 'done' event", async () => {
    const src = await startAndGetSource();
    src.emit(JSON.stringify({ type: "done", done: true }));

    const state = useOrchestrationRunStore.getState();
    expect(state.phase).toBe("COMPLETE");
    expect(state.isRunning).toBe(false);
    expect(src.closed).toBe(true);
  });

  it("ends with an error frame's message", async () => {
    const src = await startAndGetSource();
    src.emit(JSON.stringify({ type: "error", message: "The workflow timed out before completing.", done: true }));

    const state = useOrchestrationRunStore.getState();
    expect(state.lastOutcome).toBe("error");
    expect(state.logs.at(-1)).toEqual({ author: "System", text: "The workflow timed out before completing.", type: "error" });
  });

  it("reopens a failed stream after the last event it saw", async () => {
    vi.useFakeTimers();
    try {
      const src = await startAndGetSource();
      src.emit(JSON.stringify({ type: "message", author: "Worker", text: "doing" }), "3");

      src.fail();
      await vi.advanceTimersByTimeAsync(1_000);

      expect(FakeEventSource.last()).not.toBe(src);
      expect(FakeEventSource.last().url).toContain("/orchestration/stream/e1?after=3");
      expect(useOrchestrationRunStore.getState().connection).toBe("reconnecting");
      expect(useOrchestrationRunStore.getState().isRunning).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });

  it("gives up with an error once reconnecting keeps failing", async () => {
    vi.useFakeTimers();
    try {
      await startAndGetSource();
      for (let i = 0; i < 7; i++) {
        FakeEventSource.last().fail();
        await vi.advanceTimersByTimeAsync(60_000);
      }

      const state = useOrchestrationRunStore.getState();
      expect(state.phase).toBe("COMPLETE");
      expect(state.isRunning).toBe(false);
      expect(state.lastOutcome).toBe("error");
    } finally {
      vi.useRealTimers();
    }
  });

  it("reports a run that ended while nobody followed it", async () => {
    const src = await startAndGetSource();
    src.emit(JSON.stringify({ type: "gone", done: true }));

    const state = useOrchestrationRunStore.getState();
    expect(state.isRunning).toBe(false);
    expect(state.logs.at(-1)?.text).toContain("its result is in the history");
  });
});

describe("stopRun / resetRun", () => {
  it("stopRun requests backend cancellation and enters the stopping state", async () => {
    await useOrchestrationRunStore.getState().startPredefined({ workflowId: "w1", scenario: "s" });

    useOrchestrationRunStore.getState().stopRun();

    const state = useOrchestrationRunStore.getState();
    expect(state.connection).toBe("stopping");
    expect(state.isRunning).toBe(true);
    expect(orchestrationService.cancelOrchestrationRun).toHaveBeenCalledWith("e1");
  });

  it("stopRun finalizes as stopped when the backend confirms cancellation", async () => {
    await useOrchestrationRunStore.getState().startPredefined({ workflowId: "w1", scenario: "s" });
    const src = FakeEventSource.last();

    useOrchestrationRunStore.getState().stopRun();
    src.emit(JSON.stringify({ type: "cancelled", done: true }));

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

describe("surviving a reload", () => {
  it("remembers the run it follows for this tab, and forgets it once the run ends", async () => {
    await useOrchestrationRunStore.getState().startPredefined({ workflowId: "w1", scenario: "s" });
    expect(JSON.parse(sessionStorage.getItem("sentium.orchestration.activeRun") ?? "{}")).toMatchObject({ eventId: "e1" });

    FakeEventSource.last().emit(JSON.stringify({ type: "done", done: true }));

    expect(sessionStorage.getItem("sentium.orchestration.activeRun")).toBeNull();
  });

  it("re-attaches to the remembered run when the page loads, replaying it from the start", async () => {
    sessionStorage.setItem(
      "sentium.orchestration.activeRun",
      JSON.stringify({ eventId: "e-reload", isDynamicRun: true, runStartedAt: 1234 }),
    );
    vi.resetModules();
    const { useOrchestrationRunStore: reloaded } = await import("./orchestration-run-store");

    expect(reloaded.getState()).toMatchObject({ isRunning: true, isDynamicRun: true, runStartedAt: 1234 });
    await vi.waitFor(() => expect(FakeEventSource.instances.length).toBeGreaterThan(0));
    expect(FakeEventSource.last().url).toMatch(/\/orchestration\/stream\/e-reload$/);

    FakeEventSource.last().emit(JSON.stringify({ type: "message", author: "Worker", text: "replayed" }), "1");
    await applied();
    expect(reloaded.getState().logs).toEqual([{ author: "Worker", text: "replayed", type: "message" }]);
  });
});

describe("frame batching", () => {
  it("applies a burst of frames - a replay after a reload - in one go", async () => {
    await useOrchestrationRunStore.getState().startPredefined({ workflowId: "w1", scenario: "s" });
    const src = FakeEventSource.last();
    const listener = vi.fn();
    const unsubscribe = useOrchestrationRunStore.subscribe(listener);

    for (let i = 0; i < 500; i++) {
      src.emit(JSON.stringify({ type: "message", author: "Worker", text: "x" }), String(i + 1));
    }
    expect(useOrchestrationRunStore.getState().logs).toHaveLength(0);
    await applied();
    unsubscribe();

    expect(useOrchestrationRunStore.getState().logs).toEqual([{ author: "Worker", text: "x".repeat(500), type: "message" }]);
  });

  it("applies the waiting frames before the terminal one", async () => {
    await useOrchestrationRunStore.getState().startPredefined({ workflowId: "w1", scenario: "s" });
    const src = FakeEventSource.last();

    src.emit(JSON.stringify({ type: "message", author: "Worker", text: "last words" }));
    src.emit(JSON.stringify({ type: "done", done: true }));

    const state = useOrchestrationRunStore.getState();
    expect(state.logs[0]).toEqual({ author: "Worker", text: "last words", type: "message" });
    expect(state.isRunning).toBe(false);
  });
});
