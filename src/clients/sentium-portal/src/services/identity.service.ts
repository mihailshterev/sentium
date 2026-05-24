import { client } from "../api/client";
import type { Role } from "../utils/roles";

export interface UserProfile {
  id: string;
  email: string;
  firstName: string;
  lastName: string | null;
}

export interface UserListItem {
  id: string;
  email: string;
  firstName: string;
  lastName: string | null;
  roles: string[];
  isLockedOut: boolean;
}

export interface AssignRolePayload {
  userId: string;
  roleName: string;
}

export const identityService = {
  getMe: () => client.get<UserProfile>("/identity/account/me"),

  updateMe: (data: { firstName: string; lastName?: string | null; email: string }) =>
    client.put<void>("/identity/account/me", data),

  getUsers: () => client.get<UserListItem[]>("/identity/users"),

  getUser: (id: string) => client.get<UserListItem>(`/identity/users/${id}`),

  deleteUser: (id: string) => client.delete<void>(`/identity/users/${id}`),

  getRoles: () => client.get<{ name: Role; permissions: string[] }[]>("/identity/roles"),

  getUserRoles: (userId: string) => client.get<string[]>(`/identity/roles/user/${userId}`),

  assignRole: (payload: AssignRolePayload) => client.post<void>("/identity/roles/assign", payload),

  removeRole: (payload: AssignRolePayload) => client.post<void>("/identity/roles/remove", payload),
};
