import { useQuery } from "@tanstack/react-query";
import { modelQueries } from "@/features/models/api/models.queries";

/**
 * The ids of the chat models the inference server serves, plus the default model, for model pickers.
 * Shares the model catalog's query, so pickers and the Models page never fetch twice.
 */
export function useModels() {
  const { data } = useQuery(modelQueries.catalog());

  return {
    models: data?.models.map((m) => m.id) ?? [],
    defaultModel: data?.defaultModel ?? null,
  };
}
