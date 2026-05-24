import { useForm, Controller, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { X, Loader, CheckCircle, Cpu } from "lucide-react";
import styles from "../agents.module.scss";
import ModelSelector from "../../../components/ui/model-selector";
import FormField from "../../../components/ui/form-field";
import StatusMessage from "../../../components/ui/status-message";
import type { AgentRecord } from "../../../types/agents";
import { agentCreateSchema, type AgentCreateFormData } from "../../../schemas/agent.create";

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
  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<AgentCreateFormData>({
    resolver: zodResolver(agentCreateSchema),
    defaultValues: {
      name: agent.name,
      description: agent.description,
      model: agent.model || models[0] || "",
    },
  });

  const descriptionValue = useWatch({
    control,
    name: "description",
  });

  const handleFormSubmit = (data: AgentCreateFormData) => {
    onSubmit({ id: agent.id, name: data.name, description: data.description, model: data.model });
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
        <form onSubmit={handleSubmit(handleFormSubmit)} className={styles.createForm}>
          <FormField id="edit-name" label="Agent Name">
            <input
              id="edit-name"
              className={styles.fieldInput}
              type="text"
              autoComplete="off"
              spellCheck={false}
              {...register("name")}
            />
          </FormField>

          <FormField
            id="edit-description"
            label="Description"
            charCount={{ current: descriptionValue.length, max: 1000 }}
          >
            <textarea
              id="edit-description"
              className={`${styles.fieldInput} ${styles.fieldTextarea}`}
              rows={4}
              spellCheck={false}
              {...register("description")}
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
            <Controller
              name="model"
              control={control}
              render={({ field }) => (
                <ModelSelector
                  id="edit-model"
                  className={styles.fieldInput}
                  models={models}
                  value={field.value}
                  onChange={field.onChange}
                  placeholder="e.g. gemma3:1b"
                />
              )}
            />
          </FormField>

          <button
            className={`${styles.submitBtn} ${isUpdatingAgent ? styles.submitting : ""}`}
            type="submit"
            disabled={isUpdatingAgent}
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

          {errors.name && <StatusMessage variant="error" message={errors.name.message ?? "Invalid name"} />}
          {isUpdateError && <StatusMessage variant="error" message={updateAgentError?.message ?? "Unknown error"} />}
        </form>
      </div>
    </div>
  );
};

export default AgentEditModal;
