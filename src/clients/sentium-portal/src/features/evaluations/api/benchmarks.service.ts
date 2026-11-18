import { client } from "@/shared/api/client";
import type { PagedResponse } from "@/shared/types/pagination";
import type { Benchmark, BenchmarkSummary, StartBenchmarkPayload } from "../types/benchmarks";

const BASE = "/probatio/benchmarks";

export const fetchBenchmarksPaged = (page = 1, pageSize = 20): Promise<PagedResponse<BenchmarkSummary>> =>
  client.get<PagedResponse<BenchmarkSummary>>(`${BASE}?page=${page}&pageSize=${pageSize}`);

export const fetchBenchmark = (id: string): Promise<Benchmark> => client.get<Benchmark>(`${BASE}/${id}`);

export const startBenchmark = (payload: StartBenchmarkPayload): Promise<BenchmarkSummary> =>
  client.post<BenchmarkSummary>(BASE, payload);

export const cancelBenchmark = (id: string): Promise<BenchmarkSummary> => client.post<BenchmarkSummary>(`${BASE}/${id}/cancel`, {});

export const deleteBenchmark = (id: string): Promise<void> => client.delete<void>(`${BASE}/${id}`);
