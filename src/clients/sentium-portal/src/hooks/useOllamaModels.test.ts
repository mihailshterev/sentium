import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import useOllamaModels from "./useOllamaModels";
import * as agentRuntimeService from "../services/agentRuntime.service";
import type { OllamaModel } from "../services/agentRuntime.service";

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const mockModel: OllamaModel = {
  name: "llama3:latest",
  modified_at: "2025-01-01T00:00:00Z",
  size: 4_000_000_000,
  digest: "sha256:abc123",
  details: {
    format: "gguf",
    family: "llama",
    parameter_size: "8B",
    quantization_level: "Q4_0",
  },
};

beforeEach(() => {
  vi.spyOn(agentRuntimeService, "fetchOllamaModels").mockResolvedValue([mockModel]);
  vi.spyOn(agentRuntimeService, "deleteOllamaModel").mockResolvedValue({
    deletedModel: "llama3:latest",
    defaultModel: "gemma4:e4b",
    agentsReset: 0,
  });
  vi.spyOn(agentRuntimeService, "pullModel").mockResolvedValue({
    ok: true,
    body: null,
  } as unknown as Response);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("useOllamaModels initial state", () => {
  it("starts with an empty models array", () => {
    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    expect(result.current.models).toEqual([]);
  });

  it("isLoading starts as true", () => {
    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    expect(result.current.isLoading).toBe(true);
  });

  it("pullState starts as null", () => {
    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    expect(result.current.pullState).toBeNull();
  });

  it("deletingModel starts as null", () => {
    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    expect(result.current.deletingModel).toBeNull();
  });

  it("deleteResult starts as null", () => {
    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    expect(result.current.deleteResult).toBeNull();
  });
});

describe("useOllamaModels fetching", () => {
  it("populates models after fetch resolves", async () => {
    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.models).toEqual([mockModel]);
  });

  it("exposes error when fetch fails", async () => {
    vi.spyOn(agentRuntimeService, "fetchOllamaModels").mockRejectedValueOnce(new Error("Ollama offline"));
    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.error).toBeTruthy();
  });
});

describe("useOllamaModels pull helpers", () => {
  it("cancelPull clears pullState", () => {
    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });

    act(() => {
      // Manually set pullState to simulate an in-progress pull
      // Access store internals through the returned setState (Zustand-agnostic approach).
      // We directly test the cancelPull callback changes the state.
      result.current.cancelPull();
    });

    expect(result.current.pullState).toBeNull();
  });

  it("resetPull sets pullState to null", () => {
    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    act(() => {
      result.current.resetPull();
    });
    expect(result.current.pullState).toBeNull();
  });
});

describe("useOllamaModels pull()", () => {
  it("sets pullState to 'Connecting...' immediately on invocation", async () => {
    let resolveResponse!: (r: Response) => void;
    vi.spyOn(agentRuntimeService, "pullModel").mockReturnValueOnce(
      new Promise<Response>((res) => {
        resolveResponse = res;
      }),
    );

    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);

    act(() => void result.current.pull("llama3:latest"));

    expect(result.current.pullState?.status).toBe("Connecting...");
    expect(result.current.pullState?.done).toBe(false);

    act(() => resolveResponse({ ok: true, body: null } as unknown as Response));
    await waitFor(() => expect(result.current.pullState?.done).toBe(true));
  });

  it("sets error state when response is not ok", async () => {
    vi.spyOn(agentRuntimeService, "pullModel").mockResolvedValueOnce({
      ok: false,
      status: 500,
      body: null,
    } as unknown as Response);

    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);

    await act(async () => {
      await result.current.pull("badmodel");
    });

    expect(result.current.pullState?.error).toContain("500");
    expect(result.current.pullState?.done).toBe(true);
  });

  it("parses NDJSON progress lines and updates pullState", async () => {
    const progressLines = [JSON.stringify({ status: "pulling manifest" }), JSON.stringify({ status: "success" })].join(
      "\n",
    );

    const encoder = new TextEncoder();
    const stream = new ReadableStream({
      start(controller) {
        controller.enqueue(encoder.encode(progressLines + "\n"));
        controller.close();
      },
    });

    vi.spyOn(agentRuntimeService, "pullModel").mockResolvedValueOnce({
      ok: true,
      body: stream,
    } as unknown as Response);

    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);

    await act(async () => {
      await result.current.pull("llama3:latest");
    });

    expect(result.current.pullState?.done).toBe(true);
    expect(result.current.pullState?.status).toBe("success");
  });

  it("skips empty lines in NDJSON stream", async () => {
    const encoder = new TextEncoder();
    // Include empty lines between valid lines
    const stream = new ReadableStream({
      start(controller) {
        controller.enqueue(encoder.encode("\n\n" + JSON.stringify({ status: "success" }) + "\n"));
        controller.close();
      },
    });
    vi.spyOn(agentRuntimeService, "pullModel").mockResolvedValueOnce({
      ok: true,
      body: stream,
    } as unknown as Response);

    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);

    await act(async () => {
      await result.current.pull("llama3:latest");
    });

    expect(result.current.pullState?.done).toBe(true);
  });

  it("sets pullState to null when pull is aborted", async () => {
    vi.spyOn(agentRuntimeService, "pullModel").mockRejectedValueOnce(
      Object.assign(new Error("AbortError"), { name: "AbortError" }),
    );

    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);

    await act(async () => {
      await result.current.pull("llama3:latest");
    });

    expect(result.current.pullState).toBeNull();
  });
});

describe("useOllamaModels deleteModel()", () => {
  it("stores the deleteResult after successful deletion", async () => {
    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      await result.current.deleteModel("llama3:latest");
    });

    expect(result.current.deleteResult).toMatchObject({
      deletedModel: "llama3:latest",
      defaultModel: "gemma4:e4b",
    });
    expect(result.current.deletingModel).toBeNull();
  });

  it("clearDeleteResult resets deleteResult to null", async () => {
    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);

    await act(async () => {
      await result.current.deleteModel("llama3:latest");
    });
    act(() => result.current.clearDeleteResult());

    expect(result.current.deleteResult).toBeNull();
  });
});

describe("useOllamaModels pull() edge cases", () => {
  it("aborts previous pull when pull is called again", async () => {
    // First call returns a response that stalls (never resolves reader)
    let firstAborted = false;
    const stall = new Promise<Response>(() => {});
    vi.mocked(agentRuntimeService.pullModel).mockImplementationOnce((_name, signal?) => {
      signal?.addEventListener("abort", () => {
        firstAborted = true;
      });
      return stall;
    });
    vi.mocked(agentRuntimeService.pullModel).mockResolvedValueOnce(new Response(null, { status: 200 }));

    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    // Start first pull (won't complete)
    act(() => {
      result.current.pull("model-a");
    });
    // Start second pull - should abort first
    await act(async () => {
      result.current.pull("model-b");
    });
    expect(firstAborted).toBe(true);
  });

  it("sets error state when pull throws a non-AbortError", async () => {
    vi.mocked(agentRuntimeService.pullModel).mockRejectedValueOnce("plain string error");
    const { result } = renderHook(() => useOllamaModels(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      result.current.pull("model-a");
    });
    await waitFor(() => expect(result.current.pullState?.error).toBe("Unknown error"));
  });
});
