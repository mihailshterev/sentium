import { useState } from "react";
import { Plus, Loader, Cpu } from "lucide-react";
import styles from "../agents.module.scss";
import ModelSelector from "../../../components/ui/model-selector";
import FormField from "../../../components/ui/form-field";
import StatusMessage from "../../../components/ui/status-message";

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
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [model, setModel] = useState("");

  const selectedModel = model || models[0] || "";

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({
      name: name.trim(),
      description: description.trim(),
      model: selectedModel.trim(),
    });
    setName("");
    setDescription("");
    setModel("");
  };

  return (
    <form className={styles.createForm} onSubmit={handleSubmit}>
      <FormField id="agent-name" label="Agent Name">
        <input
          id="agent-name"
          className={styles.fieldInput}
          type="text"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g. ForensicsAgent"
          maxLength={255}
          required
          autoComplete="off"
          spellCheck={false}
        />
      </FormField>

      <FormField id="agent-description" label="Description" charCount={{ current: description.length, max: 1000 }}>
        <textarea
          id="agent-description"
          className={`${styles.fieldInput} ${styles.fieldTextarea}`}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          placeholder="Describe this agent's capabilities and role in the pipeline..."
          maxLength={1000}
          rows={4}
          spellCheck={false}
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
        <ModelSelector
          id="agent-model"
          className={styles.fieldInput}
          models={models}
          value={selectedModel}
          onChange={setModel}
          placeholder="e.g. gemma3:1b"
        />
      </FormField>

      <button
        className={`${styles.submitBtn} ${isCreatingAgent ? styles.submitting : ""}`}
        type="submit"
        disabled={isCreatingAgent || !name.trim()}
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

      {isCreateSuccess && <StatusMessage variant="success" message="Agent registered successfully" />}
      {isCreateError && <StatusMessage variant="error" message={createAgentError?.message ?? "Unknown error"} />}
    </form>
  );
};

export default AgentCreateForm;
