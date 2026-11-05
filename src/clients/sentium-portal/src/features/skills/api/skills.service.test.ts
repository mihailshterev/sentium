import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import * as skillsService from "./skills.service";
import { client } from "@/shared/api/client";

vi.mock("@/shared/api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api/client")>();
  return {
    ...actual,
    client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  };
});

const okResponse = (body: unknown) => ({ ok: true, status: 200, json: async () => body }) as unknown as Response;

beforeEach(() => {
  vi.clearAllMocks();
  vi.stubGlobal("fetch", vi.fn());
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("skills.service", () => {
  it("skill CRUD hits the right endpoints", async () => {
    vi.mocked(client.get).mockResolvedValue([] as never);
    vi.mocked(client.post).mockResolvedValue({} as never);
    vi.mocked(client.put).mockResolvedValue(undefined as never);
    vi.mocked(client.delete).mockResolvedValue(undefined as never);
    await skillsService.fetchBuiltInSkills();
    await skillsService.fetchSkillsPaged();
    await skillsService.createSkill({ name: "s" } as never);
    await skillsService.updateSkill("s1", { name: "s" } as never);
    await skillsService.deleteSkill("s1");
    expect(client.get).toHaveBeenCalledWith("/praxis/skills/built-in");
    expect(client.put).toHaveBeenCalledWith("/praxis/skills/s1", { name: "s" });
    expect(client.delete).toHaveBeenCalledWith("/praxis/skills/s1");
  });

  it("uploadSkillFile redirects to login on 401", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({ ok: false, status: 401, json: async () => ({}) } as unknown as Response);
    await expect(skillsService.uploadSkillFile(new File(["x"], "s.md"))).rejects.toThrow("Session expired.");
    expect(window.location.href).toContain("/login?returnUrl=");
  });

  it("uploadSkillFile returns the created skill on success", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(okResponse({ id: "s1" }));
    expect(await skillsService.uploadSkillFile(new File(["x"], "s.md"))).toEqual({ id: "s1" });
  });
});
