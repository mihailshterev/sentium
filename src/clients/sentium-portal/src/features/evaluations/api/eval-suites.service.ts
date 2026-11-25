import { client } from "@/shared/api/client";
import { pathIn, withQuery } from "@/shared/api/url";
import type { PagedResponse } from "@/shared/types/pagination";
import type {
  CreateEvalSuitePayload,
  EvalSuite,
  EvalSuiteSummary,
  EvalSuiteVersion,
  UpdateEvalSuitePayload,
} from "../types/eval-suites";

const BASE = "/probatio/suites";
const url = pathIn(BASE);

export const fetchEvalSuitesPaged = (page = 1, pageSize = 20): Promise<PagedResponse<EvalSuiteSummary>> =>
  client.get<PagedResponse<EvalSuiteSummary>>(withQuery(BASE, { page, pageSize }));

export const fetchEvalSuite = (id: string): Promise<EvalSuite> => client.get<EvalSuite>(url`/${id}`);

export const fetchEvalSuiteVersion = (id: string, version: number): Promise<EvalSuite> =>
  client.get<EvalSuite>(url`/${id}/versions/${version}`);

export const fetchEvalSuiteVersions = (id: string): Promise<EvalSuiteVersion[]> =>
  client.get<EvalSuiteVersion[]>(url`/${id}/versions`);

export const createEvalSuite = (payload: CreateEvalSuitePayload): Promise<EvalSuite> =>
  client.post<EvalSuite>(BASE, payload);

export const updateEvalSuite = (id: string, payload: UpdateEvalSuitePayload): Promise<EvalSuite> =>
  client.put<EvalSuite>(url`/${id}`, payload);

export const deleteEvalSuite = (id: string): Promise<void> => client.delete<void>(url`/${id}`);
