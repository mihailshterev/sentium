import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { identityService, type UserProfile } from "../services/identity.service";

const PROFILE_KEY = ["profile"] as const;

type UpdateProfilePayload = { firstName: string; lastName?: string | null; email: string };

const useProfile = () => {
  const queryClient = useQueryClient();

  const { data: profile = null, isLoading } = useQuery({
    queryKey: PROFILE_KEY,
    queryFn: () => identityService.getMe(),
  });

  const updateMutation = useMutation({
    mutationFn: (payload: UpdateProfilePayload) => identityService.updateMe(payload),
    onSuccess: (_data, variables) => {
      queryClient.setQueryData<UserProfile>(PROFILE_KEY, (prev) => (prev ? { ...prev, ...variables } : prev));
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
