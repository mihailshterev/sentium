import { client } from "@/shared/api/client";
import { pathIn, withQuery } from "@/shared/api/url";
import type { PagedResponse } from "@/shared/types/pagination";
import type { Evaluation, EvaluationSummary, StartEvaluationPayload } from "../types/evaluations";

const BASE = "/probatio/evaluations";
const url = pathIn(BASE);

export const fetchEvaluationsPaged = (suiteId?: string, page = 1, pageSize = 20): Promise<PagedResponse<EvaluationSummary>> =>
  client.get<PagedResponse<EvaluationSummary>>(withQuery(BASE, { page, pageSize, suiteId }));

export const fetchEvaluation = (id: string): Promise<Evaluation> => client.get<Evaluation>(url`/${id}`);

export const startEvaluation = (payload: StartEvaluationPayload): Promise<EvaluationSummary> =>
  client.post<EvaluationSummary>(BASE, payload);

export const cancelEvaluation = (id: string): Promise<EvaluationSummary> =>
  client.post<EvaluationSummary>(url`/${id}/cancel`, {});

export const deleteEvaluation = (id: string): Promise<void> => client.delete<void>(url`/${id}`);
