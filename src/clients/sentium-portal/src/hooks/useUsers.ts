import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { identityService, type AssignRolePayload } from "../services/identity.service";

const USERS_KEY = ["users"] as const;

const useUsers = () => {
  const queryClient = useQueryClient();

  const {
    data: users = [],
    isLoading,
    isFetching,
    error,
    refetch,
  } = useQuery({
    queryKey: USERS_KEY,
    queryFn: () => identityService.getUsers(),
  });

  const assignRoleMutation = useMutation({
    mutationFn: (payload: AssignRolePayload) => identityService.assignRole(payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: USERS_KEY }),
  });

  const removeRoleMutation = useMutation({
    mutationFn: (payload: AssignRolePayload) => identityService.removeRole(payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: USERS_KEY }),
  });

  const deleteUserMutation = useMutation({
    mutationFn: (userId: string) => identityService.deleteUser(userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: USERS_KEY }),
  });

  return {
    users,
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
