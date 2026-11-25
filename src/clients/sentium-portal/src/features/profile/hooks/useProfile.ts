import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { updateMe } from "@/features/auth/api/identity.service";
import { identityQueries } from "@/features/auth/api/identity.queries";
import { useAuthStore } from "@/shared/auth/state/auth-store";

type UpdateProfilePayload = { firstName: string; lastName?: string | null; userName: string };

export function useProfile() {
  const queryClient = useQueryClient();
  const updateAuthUser = useAuthStore((s) => s.updateUser);

  const { data: profile = null, isLoading } = useQuery(identityQueries.profile());

  const updateMutation = useMutation({
    mutationFn: (payload: UpdateProfilePayload) => updateMe(payload),
    onSuccess: (_data, variables) => {
      queryClient.setQueryData(identityQueries.profile().queryKey, (prev) => (prev ? { ...prev, ...variables } : prev));
      updateAuthUser({ name: variables.userName });
    },
  });

  return {
    profile,
    isLoading,
    updateProfile: updateMutation.mutateAsync,
    isSaving: updateMutation.isPending,
    saveError: updateMutation.error,
    isSaveSuccess: updateMutation.isSuccess,
    resetSave: updateMutation.reset,
  };
}
