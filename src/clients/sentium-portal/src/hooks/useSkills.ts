import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createSkill,
  deleteSkill,
  fetchBuiltInSkills,
  fetchSkills,
  updateSkill,
  uploadSkillFile,
} from "../services/agentRuntime.service";
import type { CreateSkillPayload, UpdateSkillPayload } from "../types/skills";

const SKILLS_KEY = ["skills"] as const;
const BUILT_IN_KEY = ["skills", "built-in"] as const;

export const useSkills = () => {
  const qc = useQueryClient();

  const query = useQuery({
    queryKey: SKILLS_KEY,
    queryFn: fetchSkills,
    staleTime: 30_000,
    retry: 1,
  });

  const builtInQuery = useQuery({
    queryKey: BUILT_IN_KEY,
    queryFn: fetchBuiltInSkills,
    staleTime: Infinity,
    retry: 1,
  });

  const createMutation = useMutation({
    mutationFn: (payload: CreateSkillPayload) => createSkill(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: SKILLS_KEY }),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: UpdateSkillPayload }) => updateSkill(id, payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: SKILLS_KEY }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteSkill(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: SKILLS_KEY }),
  });

  const uploadMutation = useMutation({
    mutationFn: (file: File) => uploadSkillFile(file),
    onSuccess: () => qc.invalidateQueries({ queryKey: SKILLS_KEY }),
  });

  return {
    skills: query.data ?? [],
    isLoading: query.isLoading,
    error: query.error,
    refetch: query.refetch,

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
