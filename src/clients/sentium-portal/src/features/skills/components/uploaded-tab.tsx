import { useRef, useState } from "react";
import { BookOpen, Check, Upload } from "lucide-react";
import styles from "../skills.module.css";
import { useSkills } from "@/features/skills/hooks/useSkills";
import type { AgentSkill } from "@/features/skills/types/skills";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { Input, Textarea } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { LoadMore } from "@/shared/ui/load-more";
import { StatusMessage } from "@/shared/ui/status-message";
import { SkillCard } from "./skill-card";
import { SkillListSkeleton } from "./skill-list-skeleton";

export function UploadedTab() {
  const {
    skills,
    isLoading,
    hasMore,
    loadMore,
    isLoadingMore,
    uploadSkill,
    isUploading,
    updateSkill,
    isUpdating,
    updatingId,
    deleteSkill,
    isDeleting,
    deletingId,
  } = useSkills(1);
  const uploadedSkills = skills.filter((s) => s.skillType === 1);
  const fileRef = useRef<HTMLInputElement>(null);

  const [editingId, setEditingId] = useState<string | null>(null);
  const [editForm, setEditForm] = useState({ name: "", description: "", instructions: "" });
  const [expanded, setExpanded] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) {
      return;
    }
    setUploadError(null);
    try {
      await uploadSkill(file);
    } catch (err) {
      setUploadError(err instanceof Error ? err.message : "Upload failed");
    } finally {
      if (fileRef.current) {
        fileRef.current.value = "";
      }
    }
  };

  const handleEdit = (skill: AgentSkill) => {
    setEditForm({ name: skill.name, description: skill.description, instructions: skill.instructions });
    setEditingId(skill.id);
    setSaveError(null);
  };

  const handleSave = async (id: string) => {
    setSaveError(null);
    try {
      await updateSkill({
        id,
        payload: {
          name: editForm.name.trim(),
          description: editForm.description.trim(),
          instructions: editForm.instructions.trim(),
        },
      });
      setEditingId(null);
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : "Failed to save skill");
    }
  };

  return (
    <Card padding="none" as="section">
      <CardHeader
        icon={<Upload />}
        title="Uploaded Skills"
        description="Skills loaded from Markdown (.md) files - editable after upload"
        actions={
          <>
            <Button
              variant="primary"
              size="sm"
              icon={<Upload size={15} />}
              loading={isUploading}
              onClick={() => fileRef.current?.click()}
            >
              Upload .md
            </Button>
            <input ref={fileRef} type="file" accept=".md" hidden onChange={(e) => void handleFileChange(e)} />
          </>
        }
      />

      {uploadError && (
        <div className={styles.inlineMessage}>
          <StatusMessage variant="error" message={uploadError} />
        </div>
      )}

      {isLoading ? (
        <SkillListSkeleton />
      ) : uploadedSkills.length === 0 ? (
        <EmptyState
          icon={<BookOpen />}
          title="No uploaded skills yet"
          hint="Upload a Markdown file - the content becomes the skill instructions."
        />
      ) : (
        <ul className={styles.skillList}>
          {uploadedSkills.map((skill) =>
            editingId === skill.id ? (
              <li key={skill.id} className={`${styles.skillRow} ${styles.editor}`}>
                <div className={styles.editorGrid}>
                  <FormField id={`uploaded-name-${skill.id}`} label="Name">
                    <Input
                      id={`uploaded-name-${skill.id}`}
                      value={editForm.name}
                      onChange={(e) => setEditForm((f) => ({ ...f, name: e.target.value }))}
                    />
                  </FormField>
                  <FormField id={`uploaded-description-${skill.id}`} label="Description">
                    <Input
                      id={`uploaded-description-${skill.id}`}
                      value={editForm.description}
                      onChange={(e) => setEditForm((f) => ({ ...f, description: e.target.value }))}
                    />
                  </FormField>
                </div>
                <FormField id={`uploaded-instructions-${skill.id}`} label="Instructions">
                  <Textarea
                    id={`uploaded-instructions-${skill.id}`}
                    mono
                    rows={8}
                    value={editForm.instructions}
                    onChange={(e) => setEditForm((f) => ({ ...f, instructions: e.target.value }))}
                  />
                </FormField>
                {saveError && <StatusMessage variant="error" message={saveError} />}
                <div className={styles.editorActions}>
                  <Button
                    variant="ghost"
                    onClick={() => {
                      setEditingId(null);
                      setSaveError(null);
                    }}
                  >
                    Cancel
                  </Button>
                  <Button
                    variant="primary"
                    icon={<Check size={15} />}
                    loading={isUpdating && updatingId === skill.id}
                    onClick={() => void handleSave(skill.id)}
                  >
                    Save
                  </Button>
                </div>
              </li>
            ) : (
              <SkillCard
                key={skill.id}
                skill={skill}
                expanded={expanded === skill.id}
                onToggle={() => setExpanded(expanded === skill.id ? null : skill.id)}
                onEdit={() => handleEdit(skill)}
                onDelete={() => void deleteSkill(skill.id)}
                isDeleting={isDeleting && deletingId === skill.id}
                isUpdating={isUpdating && updatingId === skill.id}
                pill="uploaded"
                pillTone="success"
                fileName={skill.fileName ?? undefined}
              />
            ),
          )}
        </ul>
      )}
      <LoadMore hasMore={hasMore} isLoading={isLoadingMore} onLoadMore={loadMore} />
    </Card>
  );
}
