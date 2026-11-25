import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createSkill,
  deleteSkill,
  fetchSkillsPaged,
  updateSkill,
  uploadSkillFile,
} from "@/features/skills/api/skills.service";
import { skillKeys, skillQueries } from "@/features/skills/api/skills.queries";
import { useInfiniteList } from "@/shared/hooks/useInfiniteList";
import type {
  AgentSkill,
  AgentSkillType,
  CreateSkillPayload,
  UpdateSkillPayload,
} from "@/features/skills/types/skills";


export const useSkills = (skillType?: AgentSkillType) => {
  const qc = useQueryClient();
  const invalidate = () => qc.invalidateQueries({ queryKey: skillKeys.all });

  const list = useInfiniteList<AgentSkill>(
    skillKeys.list(skillType),
    (page, pageSize) => fetchSkillsPaged(skillType, page, pageSize),
    { enabled: skillType !== undefined },
  );

  const builtInQuery = useQuery({ ...skillQueries.builtIn(), retry: 1 });

  const createMutation = useMutation({
    mutationFn: (payload: CreateSkillPayload) => createSkill(payload),
    onSuccess: invalidate,
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: UpdateSkillPayload }) => updateSkill(id, payload),
    onSuccess: invalidate,
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteSkill(id),
    onSuccess: invalidate,
  });

  const uploadMutation = useMutation({
    mutationFn: (file: File) => uploadSkillFile(file),
    onSuccess: invalidate,
  });

  return {
    skills: list.items,
    isLoading: list.isLoading,
    error: list.error,
    refetch: list.refetch,
    hasMore: list.hasMore,
    loadMore: list.loadMore,
    isLoadingMore: list.isLoadingMore,

    builtInSkills: builtInQuery.data ?? [],
    isBuiltInLoading: builtInQuery.isLoading,

    createSkill: createMutation.mutateAsync,
    isCreating: createMutation.isPending,

    updateSkill: updateMutation.mutateAsync,
    isUpdating: updateMutation.isPending,
    updatingId: updateMutation.variables?.id,

    deleteSkill: deleteMutation.mutateAsync,
    isDeleting: deleteMutation.isPending,
    deletingId: deleteMutation.variables,

    uploadSkill: uploadMutation.mutateAsync,
    isUploading: uploadMutation.isPending,
  };
};
