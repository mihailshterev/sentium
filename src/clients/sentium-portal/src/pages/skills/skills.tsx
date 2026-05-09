import { useState, useRef } from "react";
import {
  BookOpen,
  Bot,
  Check,
  ChevronDown,
  ChevronUp,
  Cpu,
  Loader,
  Pencil,
  Plus,
  Trash2,
  Upload,
  X,
  Zap,
} from "lucide-react";
import styles from "./skills.module.scss";
import { useSkills } from "../../hooks/useSkills";
import type { AgentSkill, BuiltInSkill, UpdateSkillPayload } from "../../types/skills";

type Tab = "built-in" | "custom" | "uploaded";

const Skills = () => {
  const [activeTab, setActiveTab] = useState<Tab>("built-in");

  return (
    <div className={styles.root}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <Zap size={18} className={styles.titleIcon} />
          <div>
            <h1 className={styles.pageTitle}>Agent Skills</h1>
            <p className={styles.pageSubtitle}>Manage built-in and custom skills that extend what agents can do</p>
          </div>
        </div>
      </div>

      <div className={styles.tabs}>
        <button
          className={`${styles.tab} ${activeTab === "built-in" ? styles.activeTab : ""}`}
          onClick={() => setActiveTab("built-in")}
        >
          <Cpu size={14} />
          Built-in
        </button>
        <button
          className={`${styles.tab} ${activeTab === "custom" ? styles.activeTab : ""}`}
          onClick={() => setActiveTab("custom")}
        >
          <Bot size={14} />
          Custom
        </button>
        <button
          className={`${styles.tab} ${activeTab === "uploaded" ? styles.activeTab : ""}`}
          onClick={() => setActiveTab("uploaded")}
        >
          <Upload size={14} />
          Uploaded
        </button>
      </div>

      <div className={styles.body}>
        {activeTab === "built-in" && <BuiltInTab />}
        {activeTab === "custom" && <CustomTab />}
        {activeTab === "uploaded" && <UploadedTab />}
      </div>
    </div>
  );
};

const BuiltInTab = () => {
  const { builtInSkills, isBuiltInLoading } = useSkills();
  const [expanded, setExpanded] = useState<string | null>(null);

  return (
    <div className={styles.card}>
      <div className={styles.cardHeader}>
        <div className={styles.cardHeaderLeft}>
          <Cpu size={15} className={styles.cardIconCyan} />
          <div>
            <p className={styles.cardTitle}>Built-in Skills</p>
            <p className={styles.cardSubtitle}>Class-based skills always available to all agents — read-only</p>
          </div>
        </div>
        <span className={styles.pillCyan}>{builtInSkills.length} skills</span>
      </div>

      <div className={styles.cardBody}>
        {isBuiltInLoading && <p className={styles.loadingText}>Loading skills…</p>}

        {!isBuiltInLoading && builtInSkills.length === 0 && (
          <div className={styles.emptyState}>
            <Cpu size={32} className={styles.emptyIcon} />
            <p className={styles.emptyTitle}>No built-in skills found</p>
          </div>
        )}

        <div className={styles.skillList}>
          {builtInSkills.map((skill: BuiltInSkill) => (
            <div key={skill.name} className={styles.skillRow}>
              <div className={styles.skillRowHeader}>
                <div className={styles.skillMeta}>
                  <span className={styles.skillName}>{skill.name}</span>
                  <span className={styles.pillCyan}>built-in</span>
                </div>
                <button
                  className={styles.btnIcon}
                  onClick={() => setExpanded(expanded === skill.name ? null : skill.name)}
                  title="Toggle instructions"
                >
                  {expanded === skill.name ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
                </button>
              </div>
              <p className={styles.skillDesc}>{skill.description}</p>
              {expanded === skill.name && <pre className={styles.instructions}>{skill.instructions}</pre>}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};

const CustomTab = () => {
  const {
    skills,
    isLoading,
    createSkill,
    isCreating,
    updateSkill,
    isUpdating,
    updatingId,
    deleteSkill,
    isDeleting,
    deletingId,
  } = useSkills();
  const customSkills = skills.filter((s) => s.skillType === 0);

  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<string | null>(null);

  const [form, setForm] = useState({ name: "", description: "", instructions: "" });

  const resetForm = () => {
    setForm({ name: "", description: "", instructions: "" });
    setShowForm(false);
    setEditingId(null);
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

    if (editingId) {
      const payload: UpdateSkillPayload = {
        name: form.name.trim(),
        description: form.description.trim(),
        instructions: form.instructions.trim(),
      };
      await updateSkill({ id: editingId, payload });
    } else {
      await createSkill({
        name: form.name.trim(),
        description: form.description.trim(),
        instructions: form.instructions.trim(),
        skillType: 0,
      });
    }
    resetForm();
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
        {isLoading && <p className={styles.loadingText}>Loading skills…</p>}

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
                placeholder="When to use this skill — seen by agents"
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
          </div>
        )}

        {!isLoading && customSkills.length === 0 && !showForm && (
          <div className={styles.emptyState}>
            <Bot size={32} className={styles.emptyIcon} />
            <p className={styles.emptyTitle}>No custom skills yet</p>
            <p className={styles.emptyDesc}>Create a skill to extend what agents can do.</p>
          </div>
        )}

        <div className={styles.skillList}>
          {customSkills.map((skill) => (
            <SkillCard
              key={skill.id}
              skill={skill}
              expanded={expanded === skill.id}
              onToggle={() => setExpanded(expanded === skill.id ? null : skill.id)}
              onEdit={() => handleEdit(skill)}
              onDelete={() => void deleteSkill(skill.id)}
              isDeleting={isDeleting && deletingId === skill.id}
              isUpdating={isUpdating && updatingId === skill.id}
              pill="custom"
              pillClass={styles.pillPurple}
            />
          ))}
        </div>
      </div>
    </div>
  );
};

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
  };

  const handleSave = async (id: string) => {
    await updateSkill({
      id,
      payload: {
        name: editForm.name.trim(),
        description: editForm.description.trim(),
        instructions: editForm.instructions.trim(),
      },
    });
    setEditingId(null);
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
        {isLoading && <p className={styles.loadingText}>Loading skills…</p>}

        {!isLoading && uploadedSkills.length === 0 && (
          <div className={styles.emptyState}>
            <BookOpen size={32} className={styles.emptyIcon} />
            <p className={styles.emptyTitle}>No uploaded skills yet</p>
            <p className={styles.emptyDesc}>Upload a Markdown file — the content becomes the skill instructions.</p>
          </div>
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
                  <button className={styles.btnGhost} onClick={() => setEditingId(null)}>
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

interface SkillCardProps {
  skill: AgentSkill;
  expanded: boolean;
  onToggle: () => void;
  onEdit: () => void;
  onDelete: () => void;
  isDeleting: boolean;
  isUpdating: boolean;
  pill: string;
  pillClass: string;
  fileName?: string;
}

const SkillCard = ({
  skill,
  expanded,
  onToggle,
  onEdit,
  onDelete,
  isDeleting,
  pill,
  pillClass,
  fileName,
}: SkillCardProps) => (
  <div className={styles.skillRow}>
    <div className={styles.skillRowHeader}>
      <div className={styles.skillMeta}>
        <span className={styles.skillName}>{skill.name}</span>
        <span className={pillClass}>{pill}</span>
        {fileName && <span className={styles.fileChip}>{fileName}</span>}
      </div>
      <div className={styles.rowActions}>
        <button className={styles.btnIcon} onClick={onEdit} title="Edit">
          <Pencil size={13} />
        </button>
        <button
          className={`${styles.btnIcon} ${styles.btnDanger}`}
          onClick={onDelete}
          disabled={isDeleting}
          title="Delete"
        >
          {isDeleting ? <Loader size={13} className={styles.spin} /> : <Trash2 size={13} />}
        </button>
        <button className={styles.btnIcon} onClick={onToggle} title="Toggle instructions">
          {expanded ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
        </button>
      </div>
    </div>
    <p className={styles.skillDesc}>{skill.description}</p>
    {expanded && <pre className={styles.instructions}>{skill.instructions}</pre>}
  </div>
);

export default Skills;
