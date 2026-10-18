import { useQuery } from "@tanstack/react-query";
import { fetchModels } from "@/features/models/api/models.service";

const MODELS_KEY = ["models"] as const;

export function useModels() {
  const { data: models = [] } = useQuery({
    queryKey: MODELS_KEY,
    queryFn: fetchModels,
  });

  return { models };
}


