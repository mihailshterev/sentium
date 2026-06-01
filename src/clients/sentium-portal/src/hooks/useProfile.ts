import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { getMe, updateMe, type UserProfile } from "../services/identity.service";
import { useAuthStore } from "../stores/auth-store";

const PROFILE_KEY = ["profile"] as const;

type UpdateProfilePayload = { firstName: string; lastName?: string | null; email: string };

const useProfile = () => {
  const queryClient = useQueryClient();
  const updateAuthUser = useAuthStore((s) => s.updateUser);

  const { data: profile = null, isLoading } = useQuery({
    queryKey: PROFILE_KEY,
    queryFn: () => getMe(),
  });

  const updateMutation = useMutation({
    mutationFn: (payload: UpdateProfilePayload) => updateMe(payload),
    onSuccess: (_data, variables) => {
      queryClient.setQueryData<UserProfile>(PROFILE_KEY, (prev) => (prev ? { ...prev, ...variables } : prev));
      const fullName = [variables.firstName, variables.lastName].filter(Boolean).join(" ");
      updateAuthUser({ name: fullName, email: variables.email });
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
};

export default useProfile;
