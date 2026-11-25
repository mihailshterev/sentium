import { queryOptions } from "@tanstack/react-query";
import { fetchModelCatalog } from "./models.service";

export const modelKeys = {
  catalog: () => ["models"] as const,
};

/** One query for the Models page and every model picker, so they never fetch twice. */
export const modelQueries = {
  catalog: () => queryOptions({ queryKey: modelKeys.catalog(), queryFn: () => fetchModelCatalog() }),
};
