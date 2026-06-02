import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getUsers,
  assignRole,
  removeRole,
  deleteUser,
  type AssignRolePayload,
  type PagedResponse,
  type UserListItem,
} from "../services/identity.service";

const USERS_KEY = (page: number, pageSize: number) => ["users", page, pageSize] as const;

const DEFAULT_PAGE_SIZE = 20;

const useUsers = () => {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const pageSize = DEFAULT_PAGE_SIZE;

  const { data, isLoading, isFetching, error, refetch } = useQuery({
    queryKey: USERS_KEY(page, pageSize),
    queryFn: () => getUsers(page, pageSize),
  });

  const users = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data?.totalPages ?? 1;

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["users"] });

  const patchUsers = (patch: (u: UserListItem) => UserListItem, userId: string) =>
    queryClient.setQueryData<PagedResponse<UserListItem>>(USERS_KEY(page, pageSize), (old) =>
      old ? { ...old, items: old.items.map((u) => (u.id === userId ? patch(u) : u)) } : old,
    );

  const assignRoleMutation = useMutation({
    mutationFn: (payload: AssignRolePayload) => assignRole(payload),
    onSuccess: (_data, payload) => {
      patchUsers((u) => ({ ...u, roles: [...u.roles, payload.roleName] }), payload.userId);
    },
    onError: invalidate,
  });

  const removeRoleMutation = useMutation({
    mutationFn: (payload: AssignRolePayload) => removeRole(payload),
    onSuccess: (_data, payload) => {
      patchUsers((u) => ({ ...u, roles: u.roles.filter((r) => r !== payload.roleName) }), payload.userId);
    },
    onError: invalidate,
  });

  const deleteUserMutation = useMutation({
    mutationFn: (userId: string) => deleteUser(userId),
    onSuccess: () => {
      invalidate();
      if (users.length === 1 && page > 1) {
        setPage((p) => p - 1);
      }
    },
  });

  return {
    users,
    totalCount,
    totalPages,
    page,
    pageSize,
    setPage,
    isLoading,
    isFetching,
    error,
    refetch,
    assignRole: assignRoleMutation.mutateAsync,
    isAssigningRole: assignRoleMutation.isPending,
    removeRole: removeRoleMutation.mutateAsync,
    isRemovingRole: removeRoleMutation.isPending,
    deleteUser: deleteUserMutation.mutateAsync,
    isDeletingUser: deleteUserMutation.isPending,
  };
};

export default useUsers;
