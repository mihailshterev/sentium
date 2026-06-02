import { useRef, useState } from "react";
import { BookOpen, Check, Loader, Upload, X } from "lucide-react";
import styles from "../skills.module.scss";
import { useSkills } from "../../../hooks/useSkills";
import type { AgentSkill } from "../../../types/skills";
import EmptyState from "../../../components/ui/empty-state";
import StatusMessage from "../../../components/ui/status-message";
import SkillCard from "./skill-card";

const UploadedTab = () => {
  const {
    skills,
    isLoading,
    uploadSkill,
    isUploading,
    updateSkill,
    isUpdating,
    updatingId,
    deleteSkill,
    isDeleting,
    deletingId,
  } = useSkills();
  const uploadedSkills = skills.filter((s) => s.skillType === 1);
  const fileRef = useRef<HTMLInputElement>(null);

  const [editingId, setEditingId] = useState<string | null>(null);
  const [editForm, setEditForm] = useState({ name: "", description: "", instructions: "" });
  const [expanded, setExpanded] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploadError(null);
    try {
      await uploadSkill(file);
    } catch (err) {
      setUploadError(err instanceof Error ? err.message : "Upload failed");
    } finally {
      if (fileRef.current) fileRef.current.value = "";
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
    <div className={styles.card}>
      <div className={styles.cardHeader}>
        <div className={styles.cardHeaderLeft}>
          <Upload size={15} className={styles.cardIconGreen} />
          <div>
            <p className={styles.cardTitle}>Uploaded Skills</p>
            <p className={styles.cardSubtitle}>Skills loaded from Markdown (.md) files — editable after upload</p>
          </div>
        </div>
        <div className={styles.headerActions}>
          {uploadError && (
            <span className={styles.errorText}>
              <X size={12} />
              {uploadError}
            </span>
          )}
          <button className={styles.btnPrimary} onClick={() => fileRef.current?.click()} disabled={isUploading}>
            {isUploading ? <Loader size={13} className={styles.spin} /> : <Upload size={13} />}
            Upload .md
          </button>
          <input
            ref={fileRef}
            type="file"
            accept=".md"
            style={{ display: "none" }}
            onChange={(e) => void handleFileChange(e)}
          />
        </div>
      </div>

      <div className={styles.cardBody}>
        {isLoading && (
          <p className={styles.loadingText}>
            <Loader size={14} className="animate-spin" />
            Loading skills…
          </p>
        )}

        {!isLoading && uploadedSkills.length === 0 && (
          <EmptyState
            icon={<BookOpen size={32} />}
            title="No uploaded skills yet"
            hint="Upload a Markdown file — the content becomes the skill instructions."
          />
        )}

        <div className={styles.skillList}>
          {uploadedSkills.map((skill) =>
            editingId === skill.id ? (
              <div key={skill.id} className={`${styles.skillRow} ${styles.skillRowEditing}`}>
                <div className={styles.formGroup}>
                  <label className={styles.label}>Name</label>
                  <input
                    className={styles.input}
                    value={editForm.name}
                    onChange={(e) => setEditForm((f) => ({ ...f, name: e.target.value }))}
                  />
                </div>
                <div className={styles.formGroup}>
                  <label className={styles.label}>Description</label>
                  <input
                    className={styles.input}
                    value={editForm.description}
                    onChange={(e) => setEditForm((f) => ({ ...f, description: e.target.value }))}
                  />
                </div>
                <div className={styles.formGroup}>
                  <label className={styles.label}>Instructions</label>
                  <textarea
                    className={styles.textarea}
                    rows={8}
                    value={editForm.instructions}
                    onChange={(e) => setEditForm((f) => ({ ...f, instructions: e.target.value }))}
                  />
                </div>
                <div className={styles.formActions}>
                  <button
                    className={styles.btnGhost}
                    onClick={() => {
                      setEditingId(null);
                      setSaveError(null);
                    }}
                  >
                    <X size={13} />
                    Cancel
                  </button>
                  <button
                    className={styles.btnPrimary}
                    onClick={() => void handleSave(skill.id)}
                    disabled={isUpdating && updatingId === skill.id}
                  >
                    {isUpdating && updatingId === skill.id ? (
                      <Loader size={13} className={styles.spin} />
                    ) : (
                      <Check size={13} />
                    )}
                    Save
                  </button>
                </div>
                {saveError && <StatusMessage variant="error" message={saveError} />}
              </div>
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
                pillClass={styles.pillGreen}
                fileName={skill.fileName ?? undefined}
              />
            ),
          )}
        </div>
      </div>
    </div>
  );
};

export default UploadedTab;
