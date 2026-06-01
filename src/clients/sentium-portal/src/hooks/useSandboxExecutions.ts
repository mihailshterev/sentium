import { useEffect, useState } from "react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { fetchExecutions } from "../services/sandbox.service";
import type { PagedResponse } from "../types/pagination";
import type { SandboxExecutionLog, SandboxLanguage, SandboxStatusFilter } from "../types/sandbox";

const POLL_INTERVAL = 5_000;
const DEFAULT_PAGE_SIZE = 20;
const SEARCH_DEBOUNCE = 300;

export const useSandboxExecutions = (pageSize = DEFAULT_PAGE_SIZE) => {
  const [page, setPage] = useState(1);
  const [status, setStatusState] = useState<SandboxStatusFilter | null>(null);
  const [language, setLanguageState] = useState<SandboxLanguage | null>(null);
  const [search, setSearchState] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");

  useEffect(() => {
    const id = setTimeout(() => setDebouncedSearch(search), SEARCH_DEBOUNCE);
    return () => clearTimeout(id);
  }, [search]);

  const setStatus = (value: SandboxStatusFilter | null) => {
    setStatusState(value);
    setPage(1);
  };
  const setLanguage = (value: SandboxLanguage | null) => {
    setLanguageState(value);
    setPage(1);
  };
  const setSearch = (value: string) => {
    setSearchState(value);
    setPage(1);
  };

  const { data, isLoading, isFetching, error, refetch } = useQuery<PagedResponse<SandboxExecutionLog>>({
    queryKey: ["sandbox-executions", page, pageSize, status, language, debouncedSearch],
    queryFn: () =>
      fetchExecutions({
        page,
        pageSize,
        status: status ?? undefined,
        language: language ?? undefined,
        search: debouncedSearch || undefined,
      }),
    refetchInterval: POLL_INTERVAL,
    placeholderData: keepPreviousData,
    retry: false,
  });

  return {
    executions: data?.items ?? [],
    totalCount: data?.totalCount ?? 0,
    totalPages: data?.totalPages ?? 1,
    page,
    setPage,
    pageSize,
    status,
    setStatus,
    language,
    setLanguage,
    search,
    setSearch,
    isLoading,
    isFetching,
    error,
    refetch,
  };
};
