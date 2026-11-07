import { useQuery } from "@tanstack/react-query";
import { fetchModelCatalog } from "@/features/models/api/models.service";
import { MODELS_QUERY_KEY } from "./useModelCatalog";

/**
 * The ids of the chat models the inference server serves, plus the default model, for model pickers.
 * Shares the model catalog's query, so pickers and the Models page never fetch twice.
 */
export function useModels() {
  const { data } = useQuery({
    queryKey: MODELS_QUERY_KEY,
    queryFn: fetchModelCatalog,
  });

  return {
    models: data?.models.map((m) => m.id) ?? [],
    defaultModel: data?.defaultModel ?? null,
  };
}
