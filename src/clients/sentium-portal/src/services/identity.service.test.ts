import { describe, it, expect, vi, beforeEach } from "vitest";
import * as identityService from "./identity.service";
import { client } from "../api/client";

vi.mock("../api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../api/client")>();
  return {
    ...actual,
    client: {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
    },
  };
});

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
});

describe("getMe()", () => {
  it("calls client.get with /identity/account/me", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({});
    await identityService.getMe();
    expect(client.get).toHaveBeenCalledWith("/identity/account/me");
  });
});

describe("updateMe()", () => {
  it("calls client.put with profile data", async () => {
    vi.mocked(client.put).mockResolvedValueOnce(undefined);
    const data = { firstName: "Alice", lastName: "Smith", email: "alice@example.com" };
    await identityService.updateMe(data);
    expect(client.put).toHaveBeenCalledWith("/identity/account/me", data);
  });

  it("accepts null lastName", async () => {
    vi.mocked(client.put).mockResolvedValueOnce(undefined);
    const data = { firstName: "Alice", lastName: null, email: "alice@example.com" };
    await identityService.updateMe(data);
    expect(client.put).toHaveBeenCalledWith("/identity/account/me", data);
  });
});

describe("getUsers()", () => {
  it("calls client.get with /identity/users", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await identityService.getUsers();
    expect(client.get).toHaveBeenCalledWith("/identity/users?page=1&pageSize=20");
  });
});

describe("getUser()", () => {
  it("calls client.get with user id in path", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({});
    await identityService.getUser("user-1");
    expect(client.get).toHaveBeenCalledWith("/identity/users/user-1");
  });
});

describe("deleteUser()", () => {
  it("calls client.delete with user id in path", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce(undefined);
    await identityService.deleteUser("user-1");
    expect(client.delete).toHaveBeenCalledWith("/identity/users/user-1");
  });
});

describe("getRoles()", () => {
  it("calls client.get with /identity/roles", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await identityService.getRoles();
    expect(client.get).toHaveBeenCalledWith("/identity/roles");
  });
});

describe("getUserRoles()", () => {
  it("calls client.get with userId in path", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await identityService.getUserRoles("user-1");
    expect(client.get).toHaveBeenCalledWith("/identity/roles/user/user-1");
  });
});

describe("assignRole()", () => {
  it("calls client.post with assign payload", async () => {
    vi.mocked(client.post).mockResolvedValueOnce(undefined);
    await identityService.assignRole({ userId: "user-1", roleName: "Member" });
    expect(client.post).toHaveBeenCalledWith("/identity/roles/assign", {
      userId: "user-1",
      roleName: "Member",
    });
  });
});

describe("removeRole()", () => {
  it("calls client.post with remove payload", async () => {
    vi.mocked(client.post).mockResolvedValueOnce(undefined);
    await identityService.removeRole({ userId: "user-1", roleName: "Member" });
    expect(client.post).toHaveBeenCalledWith("/identity/roles/remove", {
      userId: "user-1",
      roleName: "Member",
    });
  });
});
