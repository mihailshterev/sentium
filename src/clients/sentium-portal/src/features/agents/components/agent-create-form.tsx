import { useEffect } from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Plus } from "lucide-react";
import styles from "../agents.module.css";
import { Button } from "@/shared/ui/button";
import { Input, Textarea } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { ModelSelector } from "@/shared/ui/model-selector";
import { StatusMessage } from "@/shared/ui/status-message";
import { agentCreateSchema, type AgentCreateFormData } from "../schemas/agent.create";

interface AgentCreateFormProps {
  models: string[];
  isCreatingAgent: boolean;
  isCreateSuccess: boolean;
  isCreateError: boolean;
  createAgentError: Error | null | undefined;
  onSubmit: (data: { name: string; description: string; model: string }) => void;
}

export function AgentCreateForm({
  models,
  isCreatingAgent,
  isCreateSuccess,
  isCreateError,
  createAgentError,
  onSubmit,
}: AgentCreateFormProps) {
  const defaultModel = models[0] ?? "";

  const {
    register,
    handleSubmit,
    reset,
    control,
    setValue,
    watch,
    formState: { errors },
  } = useForm<AgentCreateFormData>({
    resolver: zodResolver(agentCreateSchema),
    defaultValues: {
      name: "",
      description: "",
      model: "",
    },
  });

  useEffect(() => {
    if (models.length > 0) {
      setValue("model", models[0]);
    }
  }, [models, setValue]);

  // eslint-disable-next-line react-hooks/incompatible-library
  const nameValue = watch("name") ?? "";
  const descriptionValue = watch("description") ?? "";

  const handleFormSubmit = (data: AgentCreateFormData) => {
    onSubmit({ name: data.name, description: data.description, model: data.model });
    reset({ name: "", description: "", model: defaultModel });
  };

  return (
    <form className={styles.form} onSubmit={handleSubmit(handleFormSubmit)}>
      <FormField id="agent-name" label="Agent Name">
        <Input
          id="agent-name"
          type="text"
          placeholder="e.g. ForensicsAgent"
          autoComplete="off"
          spellCheck={false}
          invalid={!!errors.name}
          {...register("name")}
        />
      </FormField>

      <FormField id="agent-description" label="Description" charCount={{ current: descriptionValue.length, max: 1000 }}>
        <Textarea
          id="agent-description"
          placeholder="Describe this agent's capabilities and role in the pipeline..."
          rows={4}
          spellCheck={false}
          {...register("description")}
        />
      </FormField>

      <FormField id="agent-model" label="Model">
        <Controller
          name="model"
          control={control}
          render={({ field }) => (
            <ModelSelector
              id="agent-model"
              models={models}
              value={field.value}
              onChange={field.onChange}
              placeholder="e.g. gemma-4-e4b"
            />
          )}
        />
      </FormField>

      <Button
        type="submit"
        variant="primary"
        fullWidth
        loading={isCreatingAgent}
        disabled={!nameValue.trim()}
        icon={<Plus size={16} />}
      >
        {isCreatingAgent ? "Registering..." : "Register Agent"}
      </Button>

      {errors.name && <StatusMessage variant="error" message={errors.name.message ?? "Invalid name"} />}
      {isCreateSuccess && <StatusMessage variant="success" message="Agent registered successfully" />}
      {isCreateError && <StatusMessage variant="error" message={createAgentError?.message ?? "Unknown error"} />}
    </form>
  );
}
