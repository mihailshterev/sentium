import { client } from "@/shared/api/client";
import type { PagedResponse } from "@/shared/types/pagination";
import type { Evaluation, EvaluationSummary, StartEvaluationPayload } from "../types/evaluations";

const BASE = "/probatio/evaluations";

export const fetchEvaluationsPaged = (suiteId?: string, page = 1, pageSize = 20): Promise<PagedResponse<EvaluationSummary>> => {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (suiteId) {
    params.set("suiteId", suiteId);
  }
  return client.get<PagedResponse<EvaluationSummary>>(`${BASE}?${params}`);
};

export const fetchEvaluation = (id: string): Promise<Evaluation> => client.get<Evaluation>(`${BASE}/${id}`);

export const startEvaluation = (payload: StartEvaluationPayload): Promise<EvaluationSummary> =>
  client.post<EvaluationSummary>(BASE, payload);

export const cancelEvaluation = (id: string): Promise<EvaluationSummary> =>
  client.post<EvaluationSummary>(`${BASE}/${id}/cancel`, {});

export const deleteEvaluation = (id: string): Promise<void> => client.delete<void>(`${BASE}/${id}`);
