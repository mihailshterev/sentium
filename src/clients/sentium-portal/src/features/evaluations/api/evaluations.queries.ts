import { queryOptions } from "@tanstack/react-query";
import { fetchBenchmark } from "./benchmarks.service";
import { fetchEvalSuite, fetchEvalSuiteVersion, fetchEvalSuiteVersions } from "./eval-suites.service";
import { fetchEvaluation } from "./evaluations.service";

export const evalSuiteKeys = {
  all: ["eval-suites"] as const,
  list: () => [...evalSuiteKeys.all, "list"] as const,
  detail: (id: string, version?: number) => [...evalSuiteKeys.all, id, version ?? "current"] as const,
  versions: (id: string) => [...evalSuiteKeys.all, id, "versions"] as const,
};

export const evaluationKeys = {
  all: ["evaluations"] as const,
  list: (suiteId?: string) => [...evaluationKeys.all, "list", suiteId ?? "all"] as const,
  detail: (id: string) => [...evaluationKeys.all, id] as const,
};

/** A Benchmark is a group of Evaluations, so anything that changes one refreshes both families. */
export const benchmarkKeys = {
  all: ["benchmarks"] as const,
  list: () => [...benchmarkKeys.all, "list"] as const,
  detail: (id: string) => [...benchmarkKeys.all, id] as const,
};

export const evaluationQueries = {
  /** `version` opens an earlier version read-only; leave it undefined for the current one. */
  suite: (id: string, version?: number) =>
    queryOptions({
      queryKey: evalSuiteKeys.detail(id, version),
      queryFn: () => (version === undefined ? fetchEvalSuite(id) : fetchEvalSuiteVersion(id, version)),
    }),
  suiteVersions: (id: string) =>
    queryOptions({ queryKey: evalSuiteKeys.versions(id), queryFn: () => fetchEvalSuiteVersions(id) }),
  evaluation: (id: string) => queryOptions({ queryKey: evaluationKeys.detail(id), queryFn: () => fetchEvaluation(id) }),
  benchmark: (id: string) => queryOptions({ queryKey: benchmarkKeys.detail(id), queryFn: () => fetchBenchmark(id) }),
};
