import { describe, expect, it, vi } from "vitest";
import { act, renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import * as api from "../api/timeline.service";
import { monthDetail, task } from "../test-fixtures";
import { timelineKeys, useTaskMutations, useYearOverview } from "./useTimeline";
import type { MonthDetail } from "../types/timeline";

function setup() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  return { client, wrapper };
}

describe("useYearOverview", () => {
  it("passes the viewer's date", async () => {
    const fetchYear = vi.spyOn(api, "fetchYear").mockResolvedValue({ year: 2026, today: "2026-11-08", months: [] });
    const { wrapper } = setup();

    const { result } = renderHook(() => useYearOverview(2026, "2026-11-08"), { wrapper });

    await waitFor(() => expect(result.current.overview?.year).toBe(2026));
    expect(fetchYear).toHaveBeenCalledWith(2026, "2026-11-08");
  });
});

describe("useTaskMutations", () => {
  it("moves a dragged task between cached months at once", async () => {
    let finish: () => void = () => {};
    vi.spyOn(api, "scheduleTask").mockImplementation(() => new Promise((resolve) => (finish = () => resolve(task()))));
    vi.spyOn(api, "fetchMonth").mockImplementation(() => new Promise(() => {}));
    const { client, wrapper } = setup();
    const moving = task();
    client.setQueryData(timelineKeys.month(2026, 11), monthDetail({ tasks: [moving] }));
    client.setQueryData(timelineKeys.month(2026, 12), monthDetail({ month: 12 }));

    const { result } = renderHook(() => useTaskMutations(), { wrapper });
    act(() => result.current.schedule.mutate({ task: moving, target: { year: 2026, month: 12, date: "2026-12-03" } }));

    await waitFor(() => {
      expect(client.getQueryData<MonthDetail>(timelineKeys.month(2026, 11))!.tasks).toHaveLength(0);
      expect(client.getQueryData<MonthDetail>(timelineKeys.month(2026, 12))!.tasks[0]).toMatchObject({
        id: "task-1",
        month: 12,
        date: "2026-12-03",
      });
    });
    finish();
  });

  it("puts the task back when the move fails", async () => {
    vi.spyOn(api, "scheduleTask").mockRejectedValue(new Error("nope"));
    vi.spyOn(api, "fetchMonth").mockImplementation(() => new Promise(() => {}));
    const { client, wrapper } = setup();
    const moving = task();
    client.setQueryData(timelineKeys.month(2026, 11), monthDetail({ tasks: [moving] }));

    const { result } = renderHook(() => useTaskMutations(), { wrapper });
    act(() => result.current.schedule.mutate({ task: moving, target: { year: 2026, month: 11, date: "2026-11-20" } }));

    await waitFor(() => expect(result.current.schedule.isError).toBe(true));
    expect(client.getQueryData<MonthDetail>(timelineKeys.month(2026, 11))!.tasks[0].date).toBe("2026-11-12");
  });

  it("ticks a task off optimistically", async () => {
    vi.spyOn(api, "setTaskStatus").mockImplementation(() => new Promise(() => {}));
    const { client, wrapper } = setup();
    const open = task();
    client.setQueryData(timelineKeys.month(2026, 11), monthDetail({ tasks: [open] }));

    const { result } = renderHook(() => useTaskMutations(), { wrapper });
    act(() => result.current.setStatus.mutate({ task: open, status: "Done" }));

    await waitFor(() => {
      const updated = client.getQueryData<MonthDetail>(timelineKeys.month(2026, 11))!.tasks[0];
      expect(updated.status).toBe("Done");
      expect(updated.completedAt).not.toBeNull();
    });
  });
});
