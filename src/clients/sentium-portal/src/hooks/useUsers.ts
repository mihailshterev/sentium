import { useMutation, useQueryClient } from "@tanstack/react-query";
import { getUsers, assignRole, removeRole, deleteUser, type AssignRolePayload } from "../services/identity.service";
import { useInfiniteList } from "./useInfiniteList";
import type { UserListItem } from "../services/identity.service";

const USERS_KEY = ["users"] as const;
const DEFAULT_PAGE_SIZE = 20;

const useUsers = () => {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: USERS_KEY });

  const list = useInfiniteList<UserListItem>(USERS_KEY, getUsers, { pageSize: DEFAULT_PAGE_SIZE });

  const assignRoleMutation = useMutation({
    mutationFn: (payload: AssignRolePayload) => assignRole(payload),
    onSuccess: invalidate,
    onError: invalidate,
  });

  const removeRoleMutation = useMutation({
    mutationFn: (payload: AssignRolePayload) => removeRole(payload),
    onSuccess: invalidate,
    onError: invalidate,
  });

  const deleteUserMutation = useMutation({
    mutationFn: (userId: string) => deleteUser(userId),
    onSuccess: invalidate,
  });

  return {
    users: list.items,
    totalCount: list.totalCount,
    hasMore: list.hasMore,
    loadMore: list.loadMore,
    isLoadingMore: list.isLoadingMore,
    isLoading: list.isLoading,
    isFetching: list.isFetching,
    error: list.error,
    refetch: list.refetch,
    assignRole: assignRoleMutation.mutateAsync,
    isAssigningRole: assignRoleMutation.isPending,
    removeRole: removeRoleMutation.mutateAsync,
    isRemovingRole: removeRoleMutation.isPending,
    deleteUser: deleteUserMutation.mutateAsync,
    isDeletingUser: deleteUserMutation.isPending,
  };
};

export default useUsers;
