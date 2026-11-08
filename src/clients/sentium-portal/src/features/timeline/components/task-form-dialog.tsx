import { Controller, useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Bot, CalendarCheck, Trash2 } from "lucide-react";
import styles from "../timeline.module.css";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Input, Select, Textarea } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { StatusMessage } from "@/shared/ui/status-message";
import { TagInput } from "@/shared/ui/tag-input";
import type { AgentRosterEntry } from "@/features/agents/types/agents";
import {
  PRIORITIES,
  STATUSES,
  STATUS_LABELS,
  TASK_LIMITS,
  taskFormSchema,
  toTaskForm,
  toTaskPayload,
  type TaskFormData,
  type TaskFormDefaults,
} from "../schemas/timeline-task.form";
import type { SaveTaskPayload, TimelineTask } from "../types/timeline";

interface TaskFormDialogProps {
  task?: TimelineTask | null;
  defaults: TaskFormDefaults;
  roster: AgentRosterEntry[];
  tagSuggestions: string[];
  onSubmit: (payload: SaveTaskPayload) => void;
  onCancel: () => void;
  onDelete?: (task: TimelineTask) => void;
  onDiscuss?: (task: TimelineTask) => void;
  isPending: boolean;
  error?: string | null;
}

export function TaskFormDialog({
  task,
  defaults,
  roster,
  tagSuggestions,
  onSubmit,
  onCancel,
  onDelete,
  onDiscuss,
  isPending,
  error,
}: TaskFormDialogProps) {
  const editing = !!task;
  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<TaskFormData>({
    resolver: zodResolver(taskFormSchema),
    defaultValues: toTaskForm(task, defaults),
  });

  const placement = useWatch({ control, name: "placement" });
  const assigned = task?.assignedAgent ?? null;
  const assignedMissing = assigned !== null && !roster.some((a) => a.name === assigned);

  return (
    <Dialog open onClose={onCancel} title={editing ? "Edit task" : "New task"} icon={<CalendarCheck />} size="lg">
      <form onSubmit={handleSubmit((data) => onSubmit(toTaskPayload(data)))} className={styles.form}>
        <FormField id="task-title" label="Title" error={errors.title?.message}>
          <Input id="task-title" placeholder="e.g. Book the dentist" invalid={!!errors.title} {...register("title")} />
        </FormField>

        <fieldset className={styles.fieldset}>
          <legend>When</legend>
          <div className={styles.segmented} role="radiogroup" aria-label="Placement">
            <label>
              <input type="radio" value="day" {...register("placement")} />
              <span>On a day</span>
            </label>
            <label>
              <input type="radio" value="month" {...register("placement")} />
              <span>Pinned to a month</span>
            </label>
          </div>
          <div className={styles.formGrid}>
            {placement === "day" ? (
              <FormField id="task-date" label="Day" error={errors.date?.message}>
                <Input id="task-date" type="date" invalid={!!errors.date} {...register("date")} />
              </FormField>
            ) : (
              <FormField id="task-month" label="Month" error={errors.month?.message}>
                <Input id="task-month" type="month" invalid={!!errors.month} {...register("month")} />
              </FormField>
            )}
            <FormField id="task-due" label="Due by" hint="Optional deadline" error={errors.dueDate?.message}>
              <Input id="task-due" type="date" invalid={!!errors.dueDate} {...register("dueDate")} />
            </FormField>
          </div>
        </fieldset>

        <div className={styles.formGrid3}>
          <FormField id="task-priority" label="Priority">
            <Select id="task-priority" {...register("priority")}>
              {PRIORITIES.map((p) => (
                <option key={p} value={p}>
                  {p}
                </option>
              ))}
            </Select>
          </FormField>
          <FormField id="task-status" label="Status">
            <Select id="task-status" {...register("status")}>
              {STATUSES.map((s) => (
                <option key={s} value={s}>
                  {STATUS_LABELS[s]}
                </option>
              ))}
            </Select>
          </FormField>
          <FormField id="task-agent" label="Assigned agent" error={errors.assignedAgent?.message}>
            <Select id="task-agent" {...register("assignedAgent")}>
              <option value="">Unassigned</option>
              {roster.map((agent) => (
                <option key={agent.name} value={agent.name}>
                  {agent.name}
                  {agent.kind === "Custom" ? " (yours)" : ""}
                </option>
              ))}
              {assignedMissing && <option value={assigned}>{assigned} (unavailable)</option>}
            </Select>
          </FormField>
        </div>

        <FormField id="task-tags" label="Tags" error={errors.tags?.message ?? errors.tags?.[0]?.message}>
          <Controller
            control={control}
            name="tags"
            render={({ field }) => (
              <TagInput
                id="task-tags"
                value={field.value}
                onChange={field.onChange}
                suggestions={tagSuggestions}
                maxTags={TASK_LIMITS.tags}
                maxLength={TASK_LIMITS.tag}
              />
            )}
          />
        </FormField>

        <FormField id="task-description" label="Details" error={errors.description?.message}>
          <Textarea id="task-description" rows={3} {...register("description")} />
        </FormField>

        {error && <StatusMessage variant="error" message={error} />}

        <div className={styles.formActions}>
          {editing && onDelete && (
            <Button variant="danger" icon={<Trash2 size={14} />} onClick={() => onDelete(task)}>
              Delete
            </Button>
          )}
          {editing && onDiscuss && (
            <Button icon={<Bot size={14} />} onClick={() => onDiscuss(task)}>
              Discuss with {task.assignedAgent ?? "planner"}
            </Button>
          )}
          <span className={styles.formSpacer} />
          <Button variant="ghost" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" loading={isPending}>
            {editing ? "Save" : "Add task"}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
