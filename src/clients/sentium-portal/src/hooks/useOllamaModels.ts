import { useState, useCallback, useRef } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { fetchOllamaModels, pullModel, deleteOllamaModel } from "../services/agentRuntime.service";
import type { OllamaModel, DeleteModelResult, PullProgress } from "../types/models";

export interface PullState {
  status: string;
  digest?: string;
  total?: number;
  completed?: number;
  error?: string;
  done: boolean;
}

const OLLAMA_MODELS_KEY = ["ollama-models"] as const;

const useOllamaModels = () => {
  const queryClient = useQueryClient();
  const {
    data: models = [],
    isLoading,
    error,
    refetch,
  } = useQuery<OllamaModel[]>({
    queryKey: OLLAMA_MODELS_KEY,
    queryFn: fetchOllamaModels,
  });

  const [pullState, setPullState] = useState<PullState | null>(null);
  const [deletingModel, setDeletingModel] = useState<string | null>(null);
  const [deleteResult, setDeleteResult] = useState<DeleteModelResult | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const pull = useCallback(
    async (name: string) => {
      if (abortRef.current) {
        abortRef.current.abort();
      }
      const controller = new AbortController();
      abortRef.current = controller;

      setPullState({ status: "Connecting...", done: false });

      try {
        const response = await pullModel(name.trim(), controller.signal);

        if (!response.ok) {
          setPullState({ status: "", error: `Request failed: ${response.status}`, done: true });
          return;
        }

        const reader = response.body?.getReader();
        if (!reader) {
          setPullState({ status: "", error: "No response body", done: true });
          return;
        }

        const decoder = new TextDecoder();
        let buffer = "";

        while (true) {
          const { done, value } = await reader.read();
          if (done) {
            break;
          }

          buffer += decoder.decode(value, { stream: true });
          const lines = buffer.split("\n");
          buffer = lines.pop() ?? "";

          for (const line of lines) {
            const trimmed = line.trim();
            if (!trimmed) {
              continue;
            }
            try {
              const progress: PullProgress = JSON.parse(trimmed);
              const isDone = progress.status === "success";
              setPullState({
                status: progress.status,
                digest: progress.digest,
                total: progress.total,
                completed: progress.completed,
                done: isDone,
              });
              if (isDone) {
                queryClient.invalidateQueries({ queryKey: OLLAMA_MODELS_KEY });
              }
            } catch {
              // skip malformed lines
            }
          }
        }
      } catch (err) {
        if ((err as Error).name === "AbortError") {
          setPullState(null);
          return;
        }
        setPullState({
          status: "",
          error: err instanceof Error ? err.message : "Unknown error",
          done: true,
        });
      } finally {
        abortRef.current = null;
      }
    },
    [queryClient],
  );

  const cancelPull = useCallback(() => {
    abortRef.current?.abort();
    abortRef.current = null;
    setPullState(null);
  }, []);

  const resetPull = useCallback(() => {
    setPullState(null);
  }, []);

  const deleteModel = useCallback(
    async (name: string) => {
      setDeletingModel(name);
      setDeleteResult(null);
      try {
        const result = await deleteOllamaModel(name);
        setDeleteResult(result);
        queryClient.invalidateQueries({ queryKey: OLLAMA_MODELS_KEY });
        queryClient.invalidateQueries({ queryKey: ["agents"] });
      } finally {
        setDeletingModel(null);
      }
    },
    [queryClient],
  );

  return {
    models,
    isLoading,
    error,
    refetch,
    pullState,
    pull,
    cancelPull,
    resetPull,
    deletingModel,
    deleteModel,
    deleteResult,
    clearDeleteResult: () => setDeleteResult(null),
  };
};

export default useOllamaModels;
