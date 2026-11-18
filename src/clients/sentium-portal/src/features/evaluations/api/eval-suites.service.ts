import { client } from "@/shared/api/client";
import type { PagedResponse } from "@/shared/types/pagination";
import type {
  CreateEvalSuitePayload,
  EvalSuite,
  EvalSuiteSummary,
  EvalSuiteVersion,
  UpdateEvalSuitePayload,
} from "../types/eval-suites";

const BASE = "/probatio/suites";

export const fetchEvalSuitesPaged = (page = 1, pageSize = 20): Promise<PagedResponse<EvalSuiteSummary>> =>
  client.get<PagedResponse<EvalSuiteSummary>>(`${BASE}?page=${page}&pageSize=${pageSize}`);

export const fetchEvalSuite = (id: string): Promise<EvalSuite> => client.get<EvalSuite>(`${BASE}/${id}`);

export const fetchEvalSuiteVersion = (id: string, version: number): Promise<EvalSuite> =>
  client.get<EvalSuite>(`${BASE}/${id}/versions/${version}`);

export const fetchEvalSuiteVersions = (id: string): Promise<EvalSuiteVersion[]> =>
  client.get<EvalSuiteVersion[]>(`${BASE}/${id}/versions`);

export const createEvalSuite = (payload: CreateEvalSuitePayload): Promise<EvalSuite> =>
  client.post<EvalSuite>(BASE, payload);

export const updateEvalSuite = (id: string, payload: UpdateEvalSuitePayload): Promise<EvalSuite> =>
  client.put<EvalSuite>(`${BASE}/${id}`, payload);

export const deleteEvalSuite = (id: string): Promise<void> => client.delete<void>(`${BASE}/${id}`);
