import { client } from "../api/client";
import type { Role } from "../utils/roles";

export interface UserProfile {
  id: string;
  email: string;
  firstName: string;
  lastName: string | null;
}

export interface UserListItem extends UserProfile {
  roles: Role[];
  isLockedOut: boolean;
}

export interface AssignRolePayload {
  userId: string;
  roleName: Role;
}

export interface PagedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export function getMe() {
  return client.get<UserProfile>("/identity/account/me");
}

export function updateMe(data: { firstName: string; lastName?: string | null; email: string }) {
  return client.put<void>("/identity/account/me", data);
}

export function getUsers(page = 1, pageSize = 20) {
  return client.get<PagedResponse<UserListItem>>(`/identity/users?page=${page}&pageSize=${pageSize}`);
}

export function getUser(id: string) {
  return client.get<UserListItem>(`/identity/users/${id}`);
}

export function deleteUser(id: string) {
  return client.delete<void>(`/identity/users/${id}`);
}

export function getRoles() {
  return client.get<{ name: Role }[]>("/identity/roles");
}

export function getUserRoles(userId: string) {
  return client.get<string[]>(`/identity/roles/user/${userId}`);
}

export function assignRole(payload: AssignRolePayload) {
  return client.post<void>("/identity/roles/assign", payload);
}

export function removeRole(payload: AssignRolePayload) {
  return client.post<void>("/identity/roles/remove", payload);
}
