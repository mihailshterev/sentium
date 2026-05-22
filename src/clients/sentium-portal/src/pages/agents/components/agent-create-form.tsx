import { useForm, Controller, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Plus, Loader, Cpu } from "lucide-react";
import styles from "../agents.module.scss";
import ModelSelector from "../../../components/ui/model-selector";
import FormField from "../../../components/ui/form-field";
import StatusMessage from "../../../components/ui/status-message";
import { agentCreateSchema, type AgentCreateFormData } from "../../../schemas/agent.create";
import { useEffect } from "react";

interface AgentCreateFormProps {
  models: string[];
  isCreatingAgent: boolean;
  isCreateSuccess: boolean;
  isCreateError: boolean;
  createAgentError: Error | null | undefined;
  onSubmit: (data: { name: string; description: string; model: string }) => void;
}

const AgentCreateForm = ({
  models,
  isCreatingAgent,
  isCreateSuccess,
  isCreateError,
  createAgentError,
  onSubmit,
}: AgentCreateFormProps) => {
  const defaultModel = models[0] ?? "";

  const {
    register,
    handleSubmit,
    reset,
    control,
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
      reset({
        name: "",
        description: "",
        model: models[0],
      });
    }
  }, [models, reset]);

  const descriptionValue = useWatch({
    control,
    name: "description",
  });

  const handleFormSubmit = (data: AgentCreateFormData) => {
    onSubmit({ name: data.name, description: data.description, model: data.model });
    reset({ name: "", description: "", model: defaultModel });
  };

  return (
    <form className={styles.createForm} onSubmit={handleSubmit(handleFormSubmit)}>
      <FormField id="agent-name" label="Agent Name">
        <input
          id="agent-name"
          className={styles.fieldInput}
          type="text"
          placeholder="e.g. ForensicsAgent"
          autoComplete="off"
          spellCheck={false}
          {...register("name")}
        />
      </FormField>

      <FormField id="agent-description" label="Description" charCount={{ current: descriptionValue.length, max: 1000 }}>
        <textarea
          id="agent-description"
          className={`${styles.fieldInput} ${styles.fieldTextarea}`}
          placeholder="Describe this agent's capabilities and role in the pipeline..."
          rows={4}
          spellCheck={false}
          {...register("description")}
        />
      </FormField>

      <FormField
        id="agent-model"
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
              id="agent-model"
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
        className={`${styles.submitBtn} ${isCreatingAgent ? styles.submitting : ""}`}
        type="submit"
        disabled={isCreatingAgent}
      >
        {isCreatingAgent ? (
          <>
            <Loader size={14} className={styles.spinIcon} />
            Registering...
          </>
        ) : (
          <>
            <Plus size={14} />
            Register Agent
          </>
        )}
      </button>

      {errors.name && <StatusMessage variant="error" message={errors.name.message ?? "Invalid name"} />}
      {isCreateSuccess && <StatusMessage variant="success" message="Agent registered successfully" />}
      {isCreateError && <StatusMessage variant="error" message={createAgentError?.message ?? "Unknown error"} />}
    </form>
  );
};

export default AgentCreateForm;
