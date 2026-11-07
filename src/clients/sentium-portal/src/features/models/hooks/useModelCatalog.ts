import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { fetchModelCatalog, loadModel, unloadModel } from "@/features/models/api/models.service";
import type { ModelsResponse } from "@/features/models/types/models";

export const MODELS_QUERY_KEY = ["models"] as const;

/** Poll this often while a model is changing state (load and unload are asynchronous on the server). */
const TRANSITION_POLL_MS = 2000;

/** Keep polling this long after a load/unload request, until the server reports the new state. */
const TRANSITION_WATCH_MS = 30_000;

type ModelAction = { model: string; kind: "load" | "unload" };

/**
 * The model catalog for the Models page: declared chat models with their runtime status, and
 * Sovereign load/unload actions. Polls while any model is loading, or shortly after an action.
 */
export function useModelCatalog() {
  const queryClient = useQueryClient();
  const [watchUntil, setWatchUntil] = useState(0);

  const { data, isLoading, isError, error, refetch } = useQuery<ModelsResponse>({
    queryKey: MODELS_QUERY_KEY,
    queryFn: fetchModelCatalog,
    refetchInterval: (query) => {
      const changing = query.state.data?.models.some((m) => m.status === "loading") ?? false;
      return changing || Date.now() < watchUntil ? TRANSITION_POLL_MS : false;
    },
  });

  const action = useMutation({
    mutationFn: ({ model, kind }: ModelAction) => (kind === "load" ? loadModel(model) : unloadModel(model)),
    onSuccess: () => setWatchUntil(Date.now() + TRANSITION_WATCH_MS),
    onSettled: () => queryClient.invalidateQueries({ queryKey: MODELS_QUERY_KEY }),
  });

  return {
    models: data?.models ?? [],
    defaultModel: data?.defaultModel ?? null,
    isLoading,
    isError,
    error,
    refetch,
    load: (model: string) => action.mutate({ model, kind: "load" }),
    unload: (model: string) => action.mutate({ model, kind: "unload" }),
    pendingModel: action.isPending ? action.variables.model : null,
    actionError: action.error,
    clearActionError: action.reset,
  };
}
