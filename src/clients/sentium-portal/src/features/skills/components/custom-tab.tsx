import { useState } from "react";
import { Bot, Check, Plus } from "lucide-react";
import styles from "../skills.module.css";
import { useSkills } from "@/features/skills/hooks/useSkills";
import type { AgentSkill } from "@/features/skills/types/skills";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { EmptyState } from "@/shared/ui/empty-state";
import { Input, Textarea } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { LoadMore } from "@/shared/ui/load-more";
import { StatusMessage } from "@/shared/ui/status-message";
import { SkillCard } from "./skill-card";
import { SkillListSkeleton } from "./skill-list-skeleton";

export function CustomTab() {
  const {
    skills,
    isLoading,
    hasMore,
    loadMore,
    isLoadingMore,
    createSkill,
    isCreating,
    updateSkill,
    isUpdating,
    updatingId,
    deleteSkill,
    isDeleting,
    deletingId,
  } = useSkills(0);
  const customSkills = skills.filter((s) => s.skillType === 0);

  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [form, setForm] = useState({ name: "", description: "", instructions: "" });

  const [skillToDelete, setSkillToDelete] = useState<{ id: string; name: string } | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  const resetForm = () => {
    setForm({ name: "", description: "", instructions: "" });
    setShowForm(false);
    setEditingId(null);
    setFormError(null);
  };

  const handleEdit = (skill: AgentSkill) => {
    setForm({ name: skill.name, description: skill.description, instructions: skill.instructions });
    setEditingId(skill.id);
    setShowForm(true);
  };

  const handleSubmit = async () => {
    if (!form.name.trim() || !form.description.trim() || !form.instructions.trim()) {
      return;
    }
    setFormError(null);
    const payload = {
      name: form.name.trim(),
      description: form.description.trim(),
      instructions: form.instructions.trim(),
    };
    try {
      if (editingId) {
        await updateSkill({ id: editingId, payload });
      } else {
        await createSkill({ ...payload, skillType: 0 });
      }
      resetForm();
    } catch (err) {
      setFormError(err instanceof Error ? err.message : "Failed to save skill");
    }
  };

  const handleConfirmDelete = async () => {
    if (!skillToDelete) {
      return;
    }
    try {
      await deleteSkill(skillToDelete.id);
    } catch (error) {
      console.error("Failed to delete skill:", error);
    } finally {
      setSkillToDelete(null);
    }
  };

  return (
    <Card padding="none" as="section">
      <CardHeader
        icon={<Bot />}
        title="Custom Skills"
        description="Skills you define manually with a name, description, and instructions"
        actions={
          !showForm && (
            <Button variant="primary" size="sm" icon={<Plus size={15} />} onClick={() => setShowForm(true)}>
              New Skill
            </Button>
          )
        }
      />

      {showForm && (
        <div className={styles.editor}>
          <p className={styles.editorTitle}>{editingId ? "Edit Skill" : "New Custom Skill"}</p>
          <div className={styles.editorGrid}>
            <FormField id="custom-skill-name" label="Name (slug)">
              <Input
                id="custom-skill-name"
                placeholder="my-custom-skill"
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
              />
            </FormField>
            <FormField id="custom-skill-description" label="Description">
              <Input
                id="custom-skill-description"
                placeholder="When to use this skill - seen by agents"
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              />
            </FormField>
          </div>
          <FormField id="custom-skill-instructions" label="Instructions">
            <Textarea
              id="custom-skill-instructions"
              mono
              placeholder="Step-by-step guidance for the agent…"
              rows={8}
              value={form.instructions}
              onChange={(e) => setForm((f) => ({ ...f, instructions: e.target.value }))}
            />
          </FormField>
          {formError && <StatusMessage variant="error" message={formError} />}
          <div className={styles.editorActions}>
            <Button variant="ghost" onClick={resetForm}>
              Cancel
            </Button>
            <Button
              variant="primary"
              icon={<Check size={15} />}
              loading={isCreating || isUpdating}
              onClick={() => void handleSubmit()}
            >
              {editingId ? "Save" : "Create"}
            </Button>
          </div>
        </div>
      )}

      {isLoading ? (
        <SkillListSkeleton />
      ) : customSkills.length === 0 && !showForm ? (
        <EmptyState icon={<Bot />} title="No custom skills yet" hint="Create a skill to extend what agents can do." />
      ) : (
        <ul className={styles.skillList}>
          {customSkills.map((skill) => (
            <SkillCard
              key={skill.id}
              skill={skill}
              expanded={expanded === skill.id}
              onToggle={() => setExpanded(expanded === skill.id ? null : skill.id)}
              onEdit={() => handleEdit(skill)}
              onDelete={() => setSkillToDelete({ id: skill.id, name: skill.name })}
              isDeleting={isDeleting && deletingId === skill.id}
              isUpdating={isUpdating && updatingId === skill.id}
              pill="custom"
              pillTone="purple"
            />
          ))}
        </ul>
      )}
      <LoadMore hasMore={hasMore} isLoading={isLoadingMore} onLoadMore={loadMore} />

      <ConfirmDialog
        open={skillToDelete !== null}
        variant="danger"
        title="Delete Custom Skill"
        description={`Are you sure you want to delete the skill "${skillToDelete?.name || ""}"? Agents will immediately lose access to this configuration.`}
        confirmLabel="Delete Skill"
        cancelLabel="Cancel"
        onConfirm={handleConfirmDelete}
        onCancel={() => setSkillToDelete(null)}
      />
    </Card>
  );
}
