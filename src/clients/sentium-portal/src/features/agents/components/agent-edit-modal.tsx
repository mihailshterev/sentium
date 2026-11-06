import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { CheckCircle, Pencil } from "lucide-react";
import styles from "../agents.module.css";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Input, Textarea } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { ModelSelector } from "@/shared/ui/model-selector";
import { StatusMessage } from "@/shared/ui/status-message";
import type { AgentRecord } from "@/features/agents/types/agents";
import { agentCreateSchema, type AgentCreateFormData } from "../schemas/agent.create";

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

export function AgentEditModal({
  agent,
  models,
  isUpdatingAgent,
  isUpdateSuccess,
  isUpdateError,
  updateAgentError,
  onSubmit,
  onClose,
}: AgentEditModalProps) {
  const {
    register,
    handleSubmit,
    control,
    watch,
    formState: { errors },
  } = useForm<AgentCreateFormData>({
    resolver: zodResolver(agentCreateSchema),
    defaultValues: {
      name: agent.name,
      description: agent.description,
      model: agent.model || models[0] || "",
    },
  });

  // eslint-disable-next-line react-hooks/incompatible-library
  const descriptionValue = watch("description") ?? "";

  const handleFormSubmit = (data: AgentCreateFormData) => {
    onSubmit({ id: agent.id, name: data.name, description: data.description, model: data.model });
  };

  return (
    <Dialog
      open
      onClose={onClose}
      title="Edit Agent"
      description="Update the name, role description or model."
      icon={<Pencil />}
    >
      <form onSubmit={handleSubmit(handleFormSubmit)} className={styles.form}>
        <FormField id="edit-name" label="Agent Name">
          <Input
            id="edit-name"
            type="text"
            autoComplete="off"
            spellCheck={false}
            invalid={!!errors.name}
            {...register("name")}
          />
        </FormField>

        <FormField
          id="edit-description"
          label="Description"
          charCount={{ current: descriptionValue.length, max: 1000 }}
        >
          <Textarea id="edit-description" rows={4} spellCheck={false} {...register("description")} />
        </FormField>

        <FormField id="edit-model" label="Model">
          <Controller
            name="model"
            control={control}
            render={({ field }) => (
              <ModelSelector
                id="edit-model"
                models={models}
                value={field.value}
                onChange={field.onChange}
                placeholder="e.g. gemma3:1b"
              />
            )}
          />
        </FormField>

        {errors.name && <StatusMessage variant="error" message={errors.name.message ?? "Invalid name"} />}
        {isUpdateError && <StatusMessage variant="error" message={updateAgentError?.message ?? "Unknown error"} />}

        <div className={styles.dialogActions}>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant="primary"
            loading={isUpdatingAgent}
            icon={isUpdateSuccess ? <CheckCircle size={16} /> : undefined}
          >
            {isUpdatingAgent ? "Saving..." : isUpdateSuccess ? "Saved" : "Save Changes"}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
