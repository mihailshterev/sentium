import { useEffect, useState } from "react";
import { fetchExecutions } from "../services/sandbox.service";
import { useInfiniteList } from "./useInfiniteList";
import type { SandboxExecutionLog, SandboxLanguage, SandboxStatusFilter } from "../types/sandbox";

const POLL_INTERVAL = 5_000;
const DEFAULT_PAGE_SIZE = 20;
const SEARCH_DEBOUNCE = 300;

export const useSandboxExecutions = (pageSize = DEFAULT_PAGE_SIZE) => {
  const [status, setStatus] = useState<SandboxStatusFilter | null>(null);
  const [language, setLanguage] = useState<SandboxLanguage | null>(null);
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");

  useEffect(() => {
    const id = setTimeout(() => setDebouncedSearch(search), SEARCH_DEBOUNCE);
    return () => clearTimeout(id);
  }, [search]);

  const list = useInfiniteList<SandboxExecutionLog>(
    ["sandbox-executions", pageSize, status, language, debouncedSearch],
    (page, ps) =>
      fetchExecutions({
        page,
        pageSize: ps,
        status: status ?? undefined,
        language: language ?? undefined,
        search: debouncedSearch || undefined,
      }),
    { refetchInterval: POLL_INTERVAL, pageSize },
  );

  return {
    executions: list.items,
    totalCount: list.totalCount,
    hasMore: list.hasMore,
    loadMore: list.loadMore,
    isLoadingMore: list.isLoadingMore,
    status,
    setStatus,
    language,
    setLanguage,
    search,
    setSearch,
    isLoading: list.isLoading,
    isFetching: list.isFetching,
    error: list.error,
    refetch: list.refetch,
  };
};
