import { useState } from "react";
import { Check, Globe, Lock, Pencil, Trash2, X } from "lucide-react";
import styles from "../knowledge-base.module.css";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { Input, Textarea } from "@/shared/ui/form-controls";

interface LearningCardProps {
  learning: {
    id: string;
    agentName: string;
    content: string;
    tags: string;
    capturedAt: string;
    isIngested: boolean;
    isGlobal: boolean;
  };
  isSaving: boolean;
  isDeleting: boolean;
  onSave: (content: string, tags: string) => void;
  onDelete: () => void;
  formatDate: (iso: string) => string;
}

export function LearningCard({ learning: l, isSaving, isDeleting, onSave, onDelete, formatDate }: LearningCardProps) {
  const [editing, setEditing] = useState(false);
  const [editContent, setEditContent] = useState(l.content);
  const [editTags, setEditTags] = useState(l.tags);
  const [isConfirmOpen, setIsConfirmOpen] = useState(false);

  const handleEdit = () => {
    setEditContent(l.content);
    setEditTags(l.tags);
    setEditing(true);
  };

  const handleSave = () => {
    if (!editContent.trim()) {
      return;
    }
    onSave(editContent.trim(), editTags.trim());
    setEditing(false);
  };

  const tags = l.tags
    ? l.tags
        .split(",")
        .map((t) => t.trim())
        .filter(Boolean)
    : [];

  return (
    <li className={`${styles.learning} ${editing ? styles.learningEditing : ""}`}>
      <div className={styles.learningHeader}>
        <div className={styles.learningMeta}>
          <span className={styles.agentName}>{l.agentName}</span>
          <span className={styles.learningDate}>{formatDate(l.capturedAt)}</span>
          <Badge
            tone={l.isGlobal ? "info" : "neutral"}
            title={l.isGlobal ? "Validated and shared with all users' agents" : "Private to your agents"}
          >
            {l.isGlobal ? <Globe size={11} aria-hidden="true" /> : <Lock size={11} aria-hidden="true" />}
            {l.isGlobal ? "Global" : "Private"}
          </Badge>
          <Badge tone={l.isIngested ? "success" : "warning"}>{l.isIngested ? "Indexed" : "Pending"}</Badge>
        </div>

        <div className={styles.learningActions}>
          {editing ? (
            <Button
              variant="ghost"
              size="sm"
              iconOnly
              icon={<X size={15} />}
              onClick={() => setEditing(false)}
              title="Cancel"
            />
          ) : (
            <Button
              variant="ghost"
              size="sm"
              iconOnly
              icon={<Pencil size={15} />}
              onClick={handleEdit}
              title="Edit learning"
              disabled={isDeleting}
            />
          )}
          <Button
            variant="ghost"
            size="sm"
            iconOnly
            icon={<Trash2 size={15} />}
            loading={isDeleting}
            disabled={isSaving}
            onClick={() => setIsConfirmOpen(true)}
            title="Delete learning"
            className={styles.deleteButton}
          />
        </div>
      </div>

      {editing ? (
        <div className={styles.editBody}>
          <label className={styles.editLabel} htmlFor={`learning-content-${l.id}`}>
            Content (markdown)
          </label>
          <Textarea
            id={`learning-content-${l.id}`}
            mono
            rows={5}
            value={editContent}
            onChange={(e) => setEditContent(e.target.value)}
            spellCheck={false}
          />
          <label className={styles.editLabel} htmlFor={`learning-tags-${l.id}`}>
            Tags (comma-separated)
          </label>
          <Input
            id={`learning-tags-${l.id}`}
            type="text"
            value={editTags}
            onChange={(e) => setEditTags(e.target.value)}
            placeholder="e.g. workflow, memory, agent"
          />
          <div className={styles.editActions}>
            <Button variant="ghost" onClick={() => setEditing(false)}>
              Cancel
            </Button>
            <Button
              variant="primary"
              icon={<Check size={15} />}
              loading={isSaving}
              disabled={!editContent.trim()}
              onClick={handleSave}
            >
              {isSaving ? "Saving…" : "Save"}
            </Button>
          </div>
        </div>
      ) : (
        <>
          <p className={styles.learningContent}>{l.content}</p>
          {tags.length > 0 && (
            <div className={styles.tags}>
              {tags.map((tag) => (
                <span key={tag} className={styles.tag}>
                  #{tag}
                </span>
              ))}
            </div>
          )}
        </>
      )}

      <ConfirmDialog
        open={isConfirmOpen}
        variant="danger"
        title="Delete Knowledge Record"
        description="Are you sure you want to delete this captured memory item? This cannot be undone and will remove it from the agent's contextual knowledge base."
        confirmLabel="Delete Item"
        cancelLabel="Cancel"
        onConfirm={() => {
          setIsConfirmOpen(false);
          onDelete();
        }}
        onCancel={() => setIsConfirmOpen(false)}
      />
    </li>
  );
}
