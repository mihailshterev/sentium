import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useSkills } from "./useSkills";
import * as agentRuntimeService from "../services/agentRuntime.service";

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const skill = { id: "s1", name: "summarize" } as never;

beforeEach(() => {
  vi.spyOn(agentRuntimeService, "fetchSkillsPaged").mockResolvedValue({
    items: [skill],
    totalCount: 1,
    page: 1,
    pageSize: 20,
    totalPages: 1,
  });
  vi.spyOn(agentRuntimeService, "fetchBuiltInSkills").mockResolvedValue([skill]);
  vi.spyOn(agentRuntimeService, "createSkill").mockResolvedValue(skill);
  vi.spyOn(agentRuntimeService, "updateSkill").mockResolvedValue(skill);
  vi.spyOn(agentRuntimeService, "deleteSkill").mockResolvedValue(undefined);
  vi.spyOn(agentRuntimeService, "uploadSkillFile").mockResolvedValue(skill);
});

describe("useSkills", () => {
  it("loads user skills and built-in skills", async () => {
    const { result } = renderHook(() => useSkills(0), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.skills).toEqual([skill]);
    await waitFor(() => expect(result.current.builtInSkills).toEqual([skill]));
  });

  it("creates a skill", async () => {
    const { result } = renderHook(() => useSkills(0), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    await act(async () => {
      await result.current.createSkill({ name: "x" } as never);
    });
    expect(agentRuntimeService.createSkill).toHaveBeenCalled();
  });

  it("updates a skill by id", async () => {
    const { result } = renderHook(() => useSkills(0), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    await act(async () => {
      await result.current.updateSkill({ id: "s1", payload: { name: "y" } as never });
    });
    expect(agentRuntimeService.updateSkill).toHaveBeenCalledWith("s1", { name: "y" });
  });

  it("deletes a skill", async () => {
    const { result } = renderHook(() => useSkills(0), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    await act(async () => {
      await result.current.deleteSkill("s1");
    });
    expect(agentRuntimeService.deleteSkill).toHaveBeenCalledWith("s1");
  });

  it("uploads a skill file", async () => {
    const { result } = renderHook(() => useSkills(0), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    const file = new File(["x"], "skill.json");
    await act(async () => {
      await result.current.uploadSkill(file);
    });
    expect(agentRuntimeService.uploadSkillFile).toHaveBeenCalledWith(file);
  });
});
