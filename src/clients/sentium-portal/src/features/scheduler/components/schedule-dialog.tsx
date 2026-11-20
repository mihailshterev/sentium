import { useState } from "react";
import { CalendarClock, Sparkles } from "lucide-react";
import styles from "../scheduler.module.css";
import { useAgentRoster } from "@/features/agents/hooks/useAgentRoster";
import { useWorkflows } from "@/features/workflows/hooks/useWorkflows";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Input, Select, Textarea } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { StatusMessage } from "@/shared/ui/status-message";
import { Tabs } from "@/shared/ui/tabs";
import { usePreApprovableTools, useSaveScheduleMutation, useScheduleTemplates } from "../hooks/useScheduler";
import type {
  CronJobRecord,
  PreApprovalTarget,
  ScheduleKind,
  ScheduleTemplate,
} from "../types/scheduler";
import { type Cadence, DEFAULT_TIME, parseCron, toCron } from "../utils/cadence";
import { CadenceBuilder } from "./cadence-builder";
import { PreApprovalList } from "./pre-approval-list";

const FORM_ID = "schedule-form";
const NAME_MAX = 120;
const PROMPT_MAX = 8000;

type RunKind = Exclude<ScheduleKind, "Script">;

const KIND_TABS = [
  { id: "Agent", label: "Agent prompt", testId: "schedule-kind-agent" },
  { id: "Workflow", label: "Saved workflow", testId: "schedule-kind-workflow" },
];

interface ScheduleDialogProps {
  /** The schedule to edit; a new one is created when absent. */
  job?: CronJobRecord;
  onClose: () => void;
}

/**
 * Mount it only while open (and keyed by the job), so its form starts from the job each time.
 */
export function ScheduleDialog({ job, onClose }: ScheduleDialogProps) {
  const editing = job !== undefined;
  const isScript = job?.kind === "Script";

  const { roster } = useAgentRoster();
  const { workflows } = useWorkflows();
  const templates = useScheduleTemplates();
  const save = useSaveScheduleMutation();

  const [kind, setKind] = useState<RunKind>(job?.kind === "Workflow" ? "Workflow" : "Agent");
  const [name, setName] = useState(job?.jobName ?? "");
  const [agentName, setAgentName] = useState(job?.kind === "Agent" ? job.agentId : "");
  const [workflowId, setWorkflowId] = useState(job?.workflowId ?? "");
  const [prompt, setPrompt] = useState(job?.prompt ?? "");
  const [cadence, setCadence] = useState<Cadence>(
    job ? parseCron(job.cronExpression) : { type: "daily", time: DEFAULT_TIME },
  );
  const [preApproved, setPreApproved] = useState<string[]>(job?.preApprovedTools ?? []);
  const [approvalsCleared, setApprovalsCleared] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const target: PreApprovalTarget | null = isScript
    ? null
    : kind === "Agent"
      ? agentName
        ? { kind, agentName, jobId: job?.jobId }
        : null
      : workflowId
        ? { kind, workflowId, jobId: job?.jobId }
        : null;
  const gated = usePreApprovableTools(target);

  /** The owner consented for one agent or workflow and one prompt; changing either asks again. */
  const clearApprovals = () => {
    if (preApproved.length > 0) {
      setPreApproved([]);
      setApprovalsCleared(true);
    }
  };

  const applyTemplate = (template: ScheduleTemplate) => {
    clearApprovals();
    setKind("Agent");
    setName(template.name);
    setAgentName(roster.some((a) => a.name === template.agentName) ? template.agentName : "");
    setPrompt(template.prompt);
    setCadence(parseCron(template.cronExpression));
  };

  const hasTarget = kind === "Agent" ? agentName !== "" : workflowId !== "";
  const canSave =
    name.trim() !== "" && toCron(cadence) !== "" && (isScript || (hasTarget && (kind === "Workflow" || prompt.trim() !== "")));

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!canSave) {
      return;
    }

    const payload = isScript
      ? { name: name.trim(), kind: "Script" as const, cronExpression: toCron(cadence) }
      : {
          name: name.trim(),
          kind,
          cronExpression: toCron(cadence),
          agentName: kind === "Agent" ? agentName : undefined,
          workflowId: kind === "Workflow" ? workflowId : undefined,
          prompt: prompt.trim(),
          preApprovedTools: preApproved.filter((t) => gated.tools.some((g) => g.name === t)),
        };

    try {
      await save.mutateAsync({ jobId: job?.jobId, payload });
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "The schedule could not be saved.");
    }
  };

  const zoneHint = isScript
    ? "Times are UTC."
    : job
      ? `Times are in ${job.timeZone}, your calendar time zone when this was saved.`
      : "Times are in your calendar time zone (Settings → Calendar).";

  return (
    <Dialog
      open
      onClose={onClose}
      size="lg"
      icon={<CalendarClock />}
      title={editing ? `Edit “${job.jobName}”` : "New schedule"}
      description={
        isScript
          ? "An agent created this script schedule; you can rename it and change when it runs."
          : "Runs with nobody present, as you, with your agents, knowledge and timeline. You get a notification when each run ends."
      }
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button
            variant="primary"
            type="submit"
            form={FORM_ID}
            loading={save.isPending}
            disabled={!canSave}
            data-testid="schedule-save"
          >
            {editing ? "Save changes" : "Create schedule"}
          </Button>
        </>
      }
    >
      <form id={FORM_ID} className={styles.form} onSubmit={(e) => void submit(e)}>
        {!editing && templates.length > 0 && (
          <div className={styles.templates} role="group" aria-label="Start from a template">
            {templates.map((t) => (
              <button
                key={t.id}
                type="button"
                className={styles.templateCard}
                onClick={() => applyTemplate(t)}
                data-testid={`schedule-template-${t.id}`}
              >
                <span className={styles.templateName}>
                  <Sparkles size={14} aria-hidden="true" /> {t.name}
                </span>
                <span className={styles.templateDescription}>{t.description}</span>
              </button>
            ))}
          </div>
        )}

        <FormField id="schedule-name" label="Name" charCount={{ current: name.length, max: NAME_MAX }}>
          <Input
            id="schedule-name"
            value={name}
            maxLength={NAME_MAX}
            placeholder="Morning briefing"
            onChange={(e) => setName(e.target.value)}
          />
        </FormField>

        {!isScript && (
          <>
            <Tabs
              tabs={KIND_TABS}
              active={kind}
              label="What runs"
              onChange={(id) => {
                clearApprovals();
                setKind(id as RunKind);
              }}
            />

            {kind === "Agent" ? (
              <FormField id="schedule-agent" label="Agent">
                <Select
                  id="schedule-agent"
                  value={agentName}
                  onChange={(e) => {
                    clearApprovals();
                    setAgentName(e.target.value);
                  }}
                >
                  <option value="">Choose an agent…</option>
                  {roster.map((a) => (
                    <option key={a.name} value={a.name}>
                      {a.name}
                    </option>
                  ))}
                </Select>
              </FormField>
            ) : (
              <FormField id="schedule-workflow" label="Workflow">
                <Select
                  id="schedule-workflow"
                  value={workflowId}
                  onChange={(e) => {
                    clearApprovals();
                    setWorkflowId(e.target.value);
                  }}
                >
                  <option value="">Choose a workflow…</option>
                  {workflows.map((w) => (
                    <option key={w.id} value={w.id}>
                      {w.name}
                    </option>
                  ))}
                </Select>
              </FormField>
            )}

            <FormField
              id="schedule-prompt"
              label={kind === "Agent" ? "Prompt" : "Input (optional)"}
              hint={kind === "Agent" ? "What the agent should do on each run." : "The scenario the workflow runs on."}
              charCount={{ current: prompt.length, max: PROMPT_MAX }}
            >
              <Textarea
                id="schedule-prompt"
                rows={6}
                value={prompt}
                maxLength={PROMPT_MAX}
                onChange={(e) => {
                  clearApprovals();
                  setPrompt(e.target.value);
                }}
              />
            </FormField>
          </>
        )}

        <CadenceBuilder value={cadence} onChange={setCadence} zoneHint={zoneHint} />

        {!isScript && (
          <FormField
            label="Pre-approved tools"
            hint="Nobody can approve during a scheduled run: unchecked tools are refused and the agent says what it could not do. Sentinel still checks every call."
          >
            {approvalsCleared && (
              <StatusMessage
                variant="info"
                message="You changed what runs, so the earlier pre-approvals were cleared. Choose them again."
              />
            )}
            <PreApprovalList
              tools={gated.tools}
              selected={preApproved}
              onChange={setPreApproved}
              isLoading={gated.isLoading}
              hasTarget={target !== null}
            />
          </FormField>
        )}

        {error && <StatusMessage variant="error" message={error} />}
      </form>
    </Dialog>
  );
}
