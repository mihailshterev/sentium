import { useMutation, useQueryClient } from "@tanstack/react-query";
import { deleteAvatar, uploadAvatar } from "@/features/auth/api/identity.service";
import { identityKeys, identityQueries } from "@/features/auth/api/identity.queries";
import { toAvatarImage } from "../lib/resize-image";

export function useAvatar() {
  const queryClient = useQueryClient();

  const applyAvatar = (avatarUrl: string | null) => {
    queryClient.setQueryData(identityQueries.profile().queryKey, (prev) => (prev ? { ...prev, avatarUrl } : prev));
    void queryClient.invalidateQueries({ queryKey: identityKeys.users() });
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
