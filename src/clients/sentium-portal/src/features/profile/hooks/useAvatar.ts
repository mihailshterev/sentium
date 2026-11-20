import { useMutation, useQueryClient } from "@tanstack/react-query";
import { deleteAvatar, uploadAvatar, type UserProfile } from "@/features/auth/api/identity.service";
import { toAvatarImage } from "../lib/resize-image";

const PROFILE_KEY = ["profile"] as const;
const USERS_KEY = ["users"] as const;

export function useAvatar() {
  const queryClient = useQueryClient();

  const applyAvatar = (avatarUrl: string | null) => {
    queryClient.setQueryData<UserProfile>(PROFILE_KEY, (prev) => (prev ? { ...prev, avatarUrl } : prev));
    void queryClient.invalidateQueries({ queryKey: USERS_KEY });
  };

  const uploadMutation = useMutation({
    mutationFn: async (file: File) => uploadAvatar(await toAvatarImage(file)),
    onSuccess: ({ avatarUrl }) => applyAvatar(avatarUrl),
  });

  const removeMutation = useMutation({
    mutationFn: deleteAvatar,
    onSuccess: () => applyAvatar(null),
  });

  return {
    uploadAvatar: uploadMutation.mutate,
    removeAvatar: () => removeMutation.mutate(),
    isUploading: uploadMutation.isPending,
    isRemoving: removeMutation.isPending,
    error: uploadMutation.error ?? removeMutation.error,
  };
}
