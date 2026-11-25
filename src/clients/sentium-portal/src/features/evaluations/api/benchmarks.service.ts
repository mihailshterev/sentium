import { client } from "@/shared/api/client";
import { pathIn, withQuery } from "@/shared/api/url";
import type { PagedResponse } from "@/shared/types/pagination";
import type { Benchmark, BenchmarkSummary, StartBenchmarkPayload } from "../types/benchmarks";

const BASE = "/probatio/benchmarks";
const url = pathIn(BASE);

export const fetchBenchmarksPaged = (page = 1, pageSize = 20): Promise<PagedResponse<BenchmarkSummary>> =>
  client.get<PagedResponse<BenchmarkSummary>>(withQuery(BASE, { page, pageSize }));

export const fetchBenchmark = (id: string): Promise<Benchmark> => client.get<Benchmark>(url`/${id}`);

export const startBenchmark = (payload: StartBenchmarkPayload): Promise<BenchmarkSummary> =>
  client.post<BenchmarkSummary>(BASE, payload);

export const cancelBenchmark = (id: string): Promise<BenchmarkSummary> => client.post<BenchmarkSummary>(url`/${id}/cancel`, {});

export const deleteBenchmark = (id: string): Promise<void> => client.delete<void>(url`/${id}`);
