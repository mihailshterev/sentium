import { useState } from "react";
import { X, Loader, CheckCircle, Cpu } from "lucide-react";
import styles from "../agents.module.scss";
import ModelSelector from "../../../components/ui/model-selector";
import FormField from "../../../components/ui/form-field";
import StatusMessage from "../../../components/ui/status-message";
import type { AgentRecord } from "../../../types/agents";

interface AgentEditModalProps {
  agent: AgentRecord;
  models: string[];
  isUpdatingAgent: boolean;
  isUpdateSuccess: boolean;
  isUpdateError: boolean;
  updateAgentError: Error | null | undefined;
  onSubmit: (data: { id: string; name: string; description: string; model: string }) => void;
  onClose: () => void;
}

const AgentEditModal = ({
  agent,
  models,
  isUpdatingAgent,
  isUpdateSuccess,
  isUpdateError,
  updateAgentError,
  onSubmit,
  onClose,
}: AgentEditModalProps) => {
  const [editName, setEditName] = useState(agent.name);
  const [editDescription, setEditDescription] = useState(agent.description);
  const [editModel, setEditModel] = useState(agent.model || models[0] || "");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({
      id: agent.id,
      name: editName.trim(),
      description: editDescription.trim(),
      model: editModel.trim(),
    });
  };

  return (
    <div className={styles.modalOverlay} onClick={onClose}>
      <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
        <div className={styles.modalHeader}>
          <span>Edit Agent</span>
          <button className={styles.modalClose} onClick={onClose}>
            <X size={14} />
          </button>
        </div>
        <form onSubmit={handleSubmit} className={styles.createForm}>
          <FormField id="edit-name" label="Agent Name">
            <input
              id="edit-name"
              className={styles.fieldInput}
              type="text"
              value={editName}
              onChange={(e) => setEditName(e.target.value)}
              maxLength={255}
              required
              autoComplete="off"
              spellCheck={false}
            />
          </FormField>

          <FormField
            id="edit-description"
            label="Description"
            charCount={{ current: editDescription.length, max: 1000 }}
          >
            <textarea
              id="edit-description"
              className={`${styles.fieldInput} ${styles.fieldTextarea}`}
              value={editDescription}
              onChange={(e) => setEditDescription(e.target.value)}
              maxLength={1000}
              rows={4}
              spellCheck={false}
            />
          </FormField>

          <FormField
            id="edit-model"
            label={
              <>
                <Cpu size={12} /> Model
              </>
            }
          >
            <ModelSelector
              id="edit-model"
              className={styles.fieldInput}
              models={models}
              value={editModel}
              onChange={setEditModel}
              placeholder="e.g. gemma3:1b"
            />
          </FormField>

          <button
            className={`${styles.submitBtn} ${isUpdatingAgent ? styles.submitting : ""}`}
            type="submit"
            disabled={isUpdatingAgent || !editName.trim()}
          >
            {isUpdatingAgent ? (
              <>
                <Loader size={14} className={styles.spinIcon} /> Saving...
              </>
            ) : isUpdateSuccess ? (
              <>
                <CheckCircle size={14} /> Saved
              </>
            ) : (
              "Save Changes"
            )}
          </button>

          {isUpdateError && <StatusMessage variant="error" message={updateAgentError?.message ?? "Unknown error"} />}
        </form>
      </div>
    </div>
  );
};

export default AgentEditModal;
