import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createSkill,
  deleteSkill,
  fetchBuiltInSkills,
  fetchSkillsPaged,
  updateSkill,
  uploadSkillFile,
} from "../services/agentRuntime.service";
import { useInfiniteList } from "./useInfiniteList";
import type { AgentSkill, AgentSkillType, CreateSkillPayload, UpdateSkillPayload } from "../types/skills";

const SKILLS_KEY = ["skills"] as const;
const BUILT_IN_KEY = ["skills", "built-in"] as const;

export const useSkills = (skillType?: AgentSkillType) => {
  const qc = useQueryClient();
  const invalidate = () => qc.invalidateQueries({ queryKey: SKILLS_KEY });

  const list = useInfiniteList<AgentSkill>(
    [...SKILLS_KEY, skillType ?? "all"],
    (page, pageSize) => fetchSkillsPaged(skillType, page, pageSize),
    { enabled: skillType !== undefined },
  );

  const builtInQuery = useQuery({
    queryKey: BUILT_IN_KEY,
    queryFn: fetchBuiltInSkills,
    staleTime: Infinity,
    retry: 1,
  });

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
