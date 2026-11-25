import { client } from "@/shared/api/client";
import { path, withQuery } from "@/shared/api/url";
import type { Role } from "../../../shared/utils/roles";

export interface UserProfile {
  id: string;
  userName: string;
  firstName: string;
  lastName: string | null;
  /** Versioned URL of the profile picture, or null when the user has none. */
  avatarUrl: string | null;
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

export function updateMe(data: { firstName: string; lastName?: string | null; userName: string }) {
  return client.put<void>("/identity/account/me", data);
}

export function changePassword(data: { currentPassword: string; newPassword: string }) {
  return client.put<void>("/identity/account/me/password", data);
}

/** Ends every other browser session of the signed-in account; this one stays signed in. */
export function signOutOtherSessions() {
  return client.post<void>("/identity/account/me/sessions/revoke-others", null);
}

export function uploadAvatar(picture: Blob) {
  const form = new FormData();
  form.append("file", picture, "avatar");
  return client.postForm<{ avatarUrl: string }>("/identity/account/me/avatar", form);
}

export function deleteAvatar() {
  return client.delete<void>("/identity/account/me/avatar");
}

export function getUsers(page = 1, pageSize = 20) {
  return client.get<PagedResponse<UserListItem>>(withQuery("/identity/users", { page, pageSize }));
}

export function getUser(id: string) {
  return client.get<UserListItem>(path`/identity/users/${id}`);
}

export function deleteUser(id: string) {
  return client.delete<void>(path`/identity/users/${id}`);
}

/** Sovereign only: sets another user's password, unlocks the account and signs it out everywhere. */
export function resetUserPassword(id: string, newPassword: string) {
  return client.put<void>(path`/identity/users/${id}/password`, { newPassword });
}

export function unlockUser(id: string) {
  return client.post<void>(path`/identity/users/${id}/unlock`, null);
}

export function getRoles() {
  return client.get<{ name: Role }[]>("/identity/roles");
}

export function getUserRoles(userId: string) {
  return client.get<string[]>(path`/identity/roles/user/${userId}`);
}

export function assignRole(payload: AssignRolePayload) {
  return client.post<void>("/identity/roles/assign", payload);
}

export function removeRole(payload: AssignRolePayload) {
  return client.post<void>("/identity/roles/remove", payload);
}
