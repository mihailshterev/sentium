import { Controller, useForm, useWatch, type Control, type UseFormRegister } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Bot, CalendarCheck, CalendarDays, Trash2 } from "lucide-react";
import styles from "../timeline.module.css";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Input, Select, Textarea } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { StatusMessage } from "@/shared/ui/status-message";
import { TagInput } from "@/shared/ui/tag-input";
import type { AgentRosterEntry } from "@/features/agents/types/agents";
import {
  DAY_REMINDERS,
  FREQUENCIES,
  FREQUENCY_LABELS,
  KIND_LABELS,
  PRIORITIES,
  STATUSES,
  STATUS_LABELS,
  TASK_LIMITS,
  TIME_REMINDERS,
  WEEKDAYS,
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

  const kind = useWatch({ control, name: "kind" });
  const placement = useWatch({ control, name: "placement" });
  const isTask = kind === "Task";
  const noun = isTask ? "task" : "event";
  const assigned = task?.assignedAgent ?? null;
  const assignedMissing = assigned !== null && !roster.some((a) => a.name === assigned);

  return (
    <Dialog
      open
      onClose={onCancel}
      title={editing ? `Edit ${noun}` : `New ${noun}`}
      icon={isTask ? <CalendarCheck /> : <CalendarDays />}
      size="lg"
    >
      <form onSubmit={handleSubmit((data) => onSubmit(toTaskPayload(data)))} className={styles.form}>
        {!editing && (
          <div className={styles.segmented} role="radiogroup" aria-label="Kind">
            {(["Task", "CalendarEvent"] as const).map((value) => (
              <label key={value}>
                <input type="radio" value={value} {...register("kind")} />
                <span>{KIND_LABELS[value]}</span>
              </label>
            ))}
          </div>
        )}

        <FormField id="task-title" label="Title" error={errors.title?.message}>
          <Input
            id="task-title"
            placeholder={isTask ? "e.g. Book the dentist" : "e.g. Maria's birthday"}
            invalid={!!errors.title}
            {...register("title")}
          />
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
              <>
                <FormField id="task-date" label="Day" error={errors.date?.message}>
                  <Input id="task-date" type="date" invalid={!!errors.date} {...register("date")} />
                </FormField>
                <FormField id="task-time" label="Time" hint="Optional" error={errors.startTime?.message}>
                  <Input id="task-time" type="time" invalid={!!errors.startTime} {...register("startTime")} />
                </FormField>
                {!isTask && (
                  <FormField id="task-end" label="Ends" hint="Last day, for several days" error={errors.endDate?.message}>
                    <Input id="task-end" type="date" invalid={!!errors.endDate} {...register("endDate")} />
                  </FormField>
                )}
              </>
            ) : (
              <FormField id="task-month" label="Month" error={errors.month?.message}>
                <Input id="task-month" type="month" invalid={!!errors.month} {...register("month")} />
              </FormField>
            )}
            {isTask && (
              <FormField id="task-due" label="Due by" hint="Optional deadline" error={errors.dueDate?.message}>
                <Input id="task-due" type="date" invalid={!!errors.dueDate} {...register("dueDate")} />
              </FormField>
            )}
          </div>
        </fieldset>

        <RepeatFields control={control} register={register} pinned={placement === "month"} error={errors.repeat?.message ?? errors.repeatInterval?.message} occurrence={!!task?.seriesId} />

        <ReminderField control={control} register={register} pinned={placement === "month"} error={errors.reminder?.message} />

        {isTask && (
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
        )}

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
            {editing ? "Save" : `Add ${noun}`}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

interface SectionProps {
  control: Control<TaskFormData>;
  register: UseFormRegister<TaskFormData>;
  pinned: boolean;
  error?: string;
}

/** How the entry repeats; an entry pinned to a month repeats monthly or yearly only. */
function RepeatFields({ control, register, pinned, error, occurrence }: SectionProps & { occurrence: boolean }) {
  const repeat = useWatch({ control, name: "repeat" });
  const frequencies = pinned ? FREQUENCIES.filter((f) => f === "Monthly" || f === "Yearly") : FREQUENCIES;

  return (
    <fieldset className={styles.fieldset}>
      <legend>Repeat</legend>
      <div className={styles.formGrid3}>
        <FormField
          id="task-repeat"
          label="Repeat"
          error={error}
          hint={occurrence ? "Applies when you save this and all following" : undefined}
        >
          <Select id="task-repeat" {...register("repeat")}>
            <option value="">Does not repeat</option>
            {frequencies.map((f) => (
              <option key={f} value={f}>
                {FREQUENCY_LABELS[f]}
              </option>
            ))}
          </Select>
        </FormField>
        {repeat && (
          <>
            <FormField id="task-repeat-interval" label="Every">
              <Input
                id="task-repeat-interval"
                type="number"
                min={1}
                max={TASK_LIMITS.repeatInterval}
                {...register("repeatInterval")}
              />
            </FormField>
            <FormField id="task-repeat-until" label="Until" hint="Optional last day">
              <Input id="task-repeat-until" type="date" {...register("repeatUntil")} />
            </FormField>
          </>
        )}
      </div>
      {repeat === "Weekly" && (
        <Controller
          control={control}
          name="repeatWeekdays"
          render={({ field }) => (
            <div className={styles.weekdayPicker} role="group" aria-label="Weekdays">
              {WEEKDAYS.map((day) => (
                <label key={day.value}>
                  <input
                    type="checkbox"
                    aria-label={day.label}
                    checked={field.value.includes(day.value)}
                    onChange={(e) =>
                      field.onChange(
                        e.target.checked ? [...field.value, day.value] : field.value.filter((v) => v !== day.value),
                      )
                    }
                  />
                  <span aria-hidden="true">{day.label}</span>
                </label>
              ))}
            </div>
          )}
        />
      )}
    </fieldset>
  );
}

/**
 * A Reminder through the Inbox. An entry with a time is reminded that long before it; one with only a day (or pinned
 * to a month) at the start of the day.
 */
function ReminderField({ control, register, pinned, error }: SectionProps) {
  const startTime = useWatch({ control, name: "startTime" });
  const options = pinned
    ? [{ value: "0", label: "On the 1st of the month" }]
    : startTime
      ? TIME_REMINDERS
      : DAY_REMINDERS;

  return (
    <FormField id="task-reminder" label="Reminder" error={error}>
      <Select id="task-reminder" {...register("reminder")}>
        <option value="">No reminder</option>
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </Select>
    </FormField>
  );
}
