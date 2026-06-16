import { useState } from "react";
import { Bot, Check, Loader, Plus, X } from "lucide-react";
import styles from "../skills.module.scss";
import { useSkills } from "../../../hooks/useSkills";
import type { AgentSkill } from "../../../types/skills";
import EmptyState from "../../../components/ui/empty-state";
import LoadMore from "../../../components/ui/load-more";
import SkillCard from "./skill-card";
import StatusMessage from "../../../components/ui/status-message";
import ConfirmDialog from "../../../components/ui/confirm-dialog";

const CustomTab = () => {
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

  const [isConfirmOpen, setIsConfirmOpen] = useState(false);
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
    try {
      if (editingId) {
        await updateSkill({
          id: editingId,
          payload: {
            name: form.name.trim(),
            description: form.description.trim(),
            instructions: form.instructions.trim(),
          },
        });
      } else {
        await createSkill({
          name: form.name.trim(),
          description: form.description.trim(),
          instructions: form.instructions.trim(),
          skillType: 0,
        });
      }
      resetForm();
    } catch (err) {
      setFormError(err instanceof Error ? err.message : "Failed to save skill");
    }
  };

  const handleOpenDeleteConfirm = (skill: AgentSkill) => {
    setSkillToDelete({ id: skill.id, name: skill.name });
    setIsConfirmOpen(true);
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
      setIsConfirmOpen(false);
      setSkillToDelete(null);
    }
  };

  const handleCancelDelete = () => {
    setIsConfirmOpen(false);
    setSkillToDelete(null);
  };

  return (
    <div className={styles.card}>
      <div className={styles.cardHeader}>
        <div className={styles.cardHeaderLeft}>
          <Bot size={15} className={styles.cardIconPurple} />
          <div>
            <p className={styles.cardTitle}>Custom Skills</p>
            <p className={styles.cardSubtitle}>Skills you define manually with a name, description, and instructions</p>
          </div>
        </div>
        {!showForm && (
          <button className={styles.btnPrimary} onClick={() => setShowForm(true)}>
            <Plus size={13} />
            New Skill
          </button>
        )}
      </div>

      <div className={styles.cardBody}>
        {isLoading && (
          <p className={styles.loadingText}>
            <Loader size={14} className="animate-spin" />
            Loading skills…
          </p>
        )}

        {showForm && (
          <div className={styles.formCard}>
            <p className={styles.formTitle}>{editingId ? "Edit Skill" : "New Custom Skill"}</p>
            <div className={styles.formGroup}>
              <label className={styles.label}>Name (slug)</label>
              <input
                className={styles.input}
                placeholder="my-custom-skill"
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
              />
            </div>
            <div className={styles.formGroup}>
              <label className={styles.label}>Description</label>
              <input
                className={styles.input}
                placeholder="When to use this skill - seen by agents"
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              />
            </div>
            <div className={styles.formGroup}>
              <label className={styles.label}>Instructions</label>
              <textarea
                className={styles.textarea}
                placeholder="Step-by-step guidance for the agent…"
                rows={8}
                value={form.instructions}
                onChange={(e) => setForm((f) => ({ ...f, instructions: e.target.value }))}
              />
            </div>
            <div className={styles.formActions}>
              <button className={styles.btnGhost} onClick={resetForm}>
                <X size={13} />
                Cancel
              </button>
              <button
                className={styles.btnPrimary}
                onClick={() => void handleSubmit()}
                disabled={isCreating || isUpdating}
              >
                {isCreating || isUpdating ? <Loader size={13} className={styles.spin} /> : <Check size={13} />}
                {editingId ? "Save" : "Create"}
              </button>
            </div>
            {formError && <StatusMessage variant="error" message={formError} />}
          </div>
        )}

        {!isLoading && customSkills.length === 0 && !showForm && (
          <EmptyState
            icon={<Bot size={32} />}
            title="No custom skills yet"
            hint="Create a skill to extend what agents can do."
          />
        )}

        <div className={styles.skillList}>
          {customSkills.map((skill) => (
            <SkillCard
              key={skill.id}
              skill={skill}
              expanded={expanded === skill.id}
              onToggle={() => setExpanded(expanded === skill.id ? null : skill.id)}
              onEdit={() => handleEdit(skill)}
              onDelete={() => handleOpenDeleteConfirm(skill)}
              isDeleting={isDeleting && deletingId === skill.id}
              isUpdating={isUpdating && updatingId === skill.id}
              pill="custom"
              pillClass={styles.pillPurple}
            />
          ))}
          <LoadMore hasMore={hasMore} isLoading={isLoadingMore} onLoadMore={loadMore} />
        </div>
      </div>

      <ConfirmDialog
        open={isConfirmOpen}
        variant="danger"
        title="Delete Custom Skill"
        description={`Are you sure you want to delete the skill "${skillToDelete?.name || ""}"? Agents will immediately lose access to this configuration.`}
        confirmLabel="Delete Skill"
        cancelLabel="Cancel"
        onConfirm={handleConfirmDelete}
        onCancel={handleCancelDelete}
      />
    </div>
  );
};

export default CustomTab;
