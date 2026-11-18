import { useState } from "react";
import { useNavigate } from "react-router";
import { Play } from "lucide-react";
import { useAgentRoster } from "@/features/agents/hooks/useAgentRoster";
import { useModels } from "@/features/models/hooks/useModels";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Input, Select } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { ModelSelector } from "@/shared/ui/model-selector";
import { StatusMessage } from "@/shared/ui/status-message";
import { useStartEvaluation } from "../hooks/useEvaluations";

const MAX_REPETITIONS = 10;
const FORM_ID = "start-evaluation-form";

interface StartEvaluationDialogProps {
  open: boolean;
  suiteId: string;
  suiteVersion: number;
  onClose: () => void;
}

export function StartEvaluationDialog({ open, suiteId, suiteVersion, onClose }: StartEvaluationDialogProps) {
  const navigate = useNavigate();
  const { roster } = useAgentRoster();
  const { models, defaultModel } = useModels();
  const start = useStartEvaluation();

  const [agentName, setAgentName] = useState("");
  const [model, setModel] = useState("");
  const [repetitions, setRepetitions] = useState("1");
  const [error, setError] = useState<string | null>(null);

  const repetitionCount = Number(repetitions);
  const repetitionsValid = Number.isInteger(repetitionCount) && repetitionCount >= 1 && repetitionCount <= MAX_REPETITIONS;

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!agentName || !repetitionsValid) {
      return;
    }
    try {
      const started = await start.mutateAsync({
        suiteId,
        suiteVersion,
        agentName,
        model: model.trim() || null,
        repetitions: repetitionCount,
      });
      onClose();
      navigate(`/evaluations/${started.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to start the Evaluation.");
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      icon={<Play />}
      title="Evaluate an agent"
      description={`Runs every case of version ${suiteVersion} against the agent. Tools are never executed: calls are answered with the cases' canned results.`}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button
            variant="primary"
            type="submit"
            form={FORM_ID}
            icon={<Play size={15} />}
            loading={start.isPending}
            disabled={!agentName || !repetitionsValid}
          >
            Start
          </Button>
        </>
      }
    >
      <form id={FORM_ID} onSubmit={(e) => void submit(e)}>
        <FormField id="evaluation-agent" label="Agent">
          <Select id="evaluation-agent" value={agentName} onChange={(e) => setAgentName(e.target.value)}>
            <option value="">Choose an agent…</option>
            {roster.map((a) => (
              <option key={a.name} value={a.name}>
                {a.name}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField id="evaluation-model" label="Model" hint="Leave empty for the default model.">
          <ModelSelector
            id="evaluation-model"
            models={models}
            value={model}
            onChange={setModel}
            placeholder={defaultModel ? `Default (${defaultModel})` : "Default model"}
          />
        </FormField>
        <FormField
          id="evaluation-repetitions"
          label="Repetitions"
          hint={`A case passes only if it passes every repetition (1-${MAX_REPETITIONS}).`}
          error={repetitionsValid ? undefined : `Enter a whole number from 1 to ${MAX_REPETITIONS}.`}
        >
          <Input
            id="evaluation-repetitions"
            inputMode="numeric"
            value={repetitions}
            onChange={(e) => setRepetitions(e.target.value)}
          />
        </FormField>
        {error && <StatusMessage variant="error" message={error} />}
      </form>
    </Dialog>
  );
}
