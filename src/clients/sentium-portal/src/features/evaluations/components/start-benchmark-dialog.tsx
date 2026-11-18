import { useState } from "react";
import { useNavigate } from "react-router";
import { Columns3 } from "lucide-react";
import styles from "../evaluations.module.css";
import { useAgentRoster } from "@/features/agents/hooks/useAgentRoster";
import { useModels } from "@/features/models/hooks/useModels";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Input, Select } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { StatusMessage } from "@/shared/ui/status-message";
import { useStartBenchmark } from "../hooks/useBenchmarks";
import { MAX_BENCHMARK_MODELS, MIN_BENCHMARK_MODELS } from "../types/benchmarks";

const MAX_REPETITIONS = 10;
const FORM_ID = "start-benchmark-form";

interface StartBenchmarkDialogProps {
  open: boolean;
  suiteId: string;
  suiteVersion: number;
  onClose: () => void;
}

export function StartBenchmarkDialog({ open, suiteId, suiteVersion, onClose }: StartBenchmarkDialogProps) {
  const navigate = useNavigate();
  const { roster } = useAgentRoster();
  const { models } = useModels();
  const start = useStartBenchmark();

  const [agentName, setAgentName] = useState("");
  const [picked, setPicked] = useState<string[]>([]);
  const [repetitions, setRepetitions] = useState("1");
  const [error, setError] = useState<string | null>(null);

  const repetitionCount = Number(repetitions);
  const repetitionsValid = Number.isInteger(repetitionCount) && repetitionCount >= 1 && repetitionCount <= MAX_REPETITIONS;
  const modelsValid = picked.length >= MIN_BENCHMARK_MODELS && picked.length <= MAX_BENCHMARK_MODELS;
  const canStart = agentName !== "" && modelsValid && repetitionsValid;

  const toggle = (model: string, on: boolean) =>
    setPicked((prev) => (on ? [...prev, model] : prev.filter((m) => m !== model)));

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!canStart) {
      return;
    }
    try {
      const started = await start.mutateAsync({ suiteId, suiteVersion, agentName, models: picked, repetitions: repetitionCount });
      onClose();
      navigate(`/evaluations/benchmarks/${started.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to start the Benchmark.");
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      icon={<Columns3 />}
      title="Benchmark models"
      description={`Evaluates the agent on version ${suiteVersion} once per model, one after another, and compares them side by side. Every model is graded by the same Judge.`}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button
            variant="primary"
            type="submit"
            form={FORM_ID}
            icon={<Columns3 size={15} />}
            loading={start.isPending}
            disabled={!canStart}
          >
            Start
          </Button>
        </>
      }
    >
      <form id={FORM_ID} onSubmit={(e) => void submit(e)}>
        <FormField id="benchmark-agent" label="Agent">
          <Select id="benchmark-agent" value={agentName} onChange={(e) => setAgentName(e.target.value)}>
            <option value="">Choose an agent…</option>
            {roster.map((a) => (
              <option key={a.name} value={a.name}>
                {a.name}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField
          label="Models"
          hint={`Pick ${MIN_BENCHMARK_MODELS} to ${MAX_BENCHMARK_MODELS}. Each runs the whole suite, so more models take longer.`}
        >
          {models.length === 0 ? (
            <p className={styles.muted}>The model server lists no models right now.</p>
          ) : (
            <div className={styles.checkGrid} role="group" aria-label="Models">
              {models.map((model) => (
                <label className={styles.checkOption} key={model}>
                  <input
                    type="checkbox"
                    checked={picked.includes(model)}
                    disabled={!picked.includes(model) && picked.length >= MAX_BENCHMARK_MODELS}
                    onChange={(e) => toggle(model, e.target.checked)}
                  />
                  {model}
                </label>
              ))}
            </div>
          )}
        </FormField>
        <FormField
          id="benchmark-repetitions"
          label="Repetitions"
          hint={`Per case and model (1-${MAX_REPETITIONS}).`}
          error={repetitionsValid ? undefined : `Enter a whole number from 1 to ${MAX_REPETITIONS}.`}
        >
          <Input
            id="benchmark-repetitions"
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
