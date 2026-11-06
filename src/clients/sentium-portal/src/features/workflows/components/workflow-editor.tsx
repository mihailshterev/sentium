import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { DndContext, closestCenter, type DragEndEvent, PointerSensor, useSensor, useSensors } from "@dnd-kit/core";
import { SortableContext, verticalListSortingStrategy, arrayMove } from "@dnd-kit/sortable";
import { Bot, CheckCircle, ListOrdered, Plus, SlidersHorizontal } from "lucide-react";
import styles from "../workflows.module.css";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { Input, SearchInput, Textarea } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { StatusMessage } from "@/shared/ui/status-message";
import { SortableAgent } from "./sortable-agent";
import type { AgentRecord } from "@/features/agents/types/agents";
import type { WorkflowRecord, SortableAgentItem } from "@/features/workflows/types/workflows";
import { workflowEditorSchema, type WorkflowEditorFormData } from "../schemas/workflow.editor";

interface WorkflowEditorProps {
  selectedWorkflow: WorkflowRecord | null;
  agents: AgentRecord[];
  isCreatingWorkflow: boolean;
  isUpdatingWorkflow: boolean;
  isCreateSuccess: boolean;
  isUpdateSuccess: boolean;
  isCreateError: boolean;
  isUpdateError: boolean;
  createWorkflowError: Error | null | undefined;
  updateWorkflowError: Error | null | undefined;
  onSubmit: (data: { name: string; description: string; agents: { agentId: string; order: number }[] }) => void;
  getAgentName: (agentId: string) => string;
  initialFormAgents: SortableAgentItem[];
  initialName: string;
  initialDescription: string;
}

export function WorkflowEditor({
  selectedWorkflow,
  agents,
  isCreatingWorkflow,
  isUpdatingWorkflow,
  isCreateSuccess,
  isUpdateSuccess,
  isCreateError,
  isUpdateError,
  createWorkflowError,
  updateWorkflowError,
  onSubmit,
  getAgentName,
  initialFormAgents,
  initialName,
  initialDescription,
}: WorkflowEditorProps) {
  const [formAgents, setFormAgents] = useState<SortableAgentItem[]>(initialFormAgents);
  const [agentSearch, setAgentSearch] = useState("");

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<WorkflowEditorFormData>({
    resolver: zodResolver(workflowEditorSchema),
    defaultValues: { name: initialName, description: initialDescription },
  });

  // eslint-disable-next-line react-hooks/incompatible-library
  const formDescription = watch("description") ?? "";
  const formName = watch("name") ?? "";

  const sensors = useSensors(useSensor(PointerSensor));

  const isPending = selectedWorkflow ? isUpdatingWorkflow : isCreatingWorkflow;
  const isSuccess = selectedWorkflow ? isUpdateSuccess : isCreateSuccess;
  const isError = selectedWorkflow ? isUpdateError : isCreateError;
  const mutationError = selectedWorkflow ? updateWorkflowError : createWorkflowError;

  const query = agentSearch.trim().toLowerCase();
  const filteredAgents = agents.filter(
    (a) =>
      !query ||
      a.name.toLowerCase().includes(query) ||
      a.model.toLowerCase().includes(query) ||
      a.description.toLowerCase().includes(query),
  );

  const addAgentToForm = (agent: AgentRecord) => {
    setFormAgents((prev) => [
      ...prev,
      {
        sortId: `${agent.id}-${Date.now()}`,
        agentId: agent.id,
        name: agent.name,
        model: agent.model,
        description: agent.description,
      },
    ]);
  };

  const removeAgentFromForm = (sortId: string) => {
    setFormAgents((prev) => prev.filter((a) => a.sortId !== sortId));
  };

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;
    if (over && active.id !== over.id) {
      setFormAgents((items) => {
        const oldIndex = items.findIndex((i) => i.sortId === active.id);
        const newIndex = items.findIndex((i) => i.sortId === over.id);
        return arrayMove(items, oldIndex, newIndex);
      });
    }
  };

  const handleFormSubmit = (data: WorkflowEditorFormData) => {
    onSubmit({
      name: data.name,
      description: data.description,
      agents: formAgents.map((a, idx) => ({ agentId: a.agentId, order: idx })),
    });
  };

  return (
    <form onSubmit={handleSubmit(handleFormSubmit)} className={styles.editor}>
      <Card padding="none" as="section" className={styles.pipelineCard}>
        <CardHeader
          icon={<ListOrdered />}
          title="Execution order"
          description="Agents run top to bottom; drag to reorder"
          actions={<Badge tone="neutral">{formAgents.length} agents</Badge>}
        />

        <div className={styles.pipelineBody}>
          {formAgents.length === 0 ? (
            <div className={styles.pipelineEmpty}>
              <span className={styles.pipelineEmptyIcon} aria-hidden="true">
                <Bot size={20} />
              </span>
              <span className={styles.pipelineEmptyTitle}>Add agents from the list</span>
              <span className={styles.pipelineEmptyHint}>Drag to reorder execution sequence</span>
              {selectedWorkflow && selectedWorkflow.agents.length > 0 && (
                <ol className={styles.previewList}>
                  {[...selectedWorkflow.agents]
                    .sort((a, b) => a.order - b.order)
                    .map((a) => (
                      <li key={a.agentId}>{getAgentName(a.agentId)}</li>
                    ))}
                </ol>
              )}
            </div>
          ) : (
            <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
              <SortableContext items={formAgents.map((a) => a.sortId)} strategy={verticalListSortingStrategy}>
                <ol className={styles.pipeline}>
                  {formAgents.map((item, idx) => (
                    <li key={item.sortId} className={styles.step}>
                      <span className={styles.stepIndex}>{idx + 1}</span>
                      <SortableAgent item={item} onRemove={removeAgentFromForm} />
                    </li>
                  ))}
                </ol>
              </SortableContext>
            </DndContext>
          )}
        </div>
      </Card>

      <div className={styles.sideColumn}>
        <Card padding="md" as="section">
          <CardHeader icon={<SlidersHorizontal />} title="Details" />
          <div className={styles.fields}>
            <FormField id="wf-name" label="Name" error={errors.name?.message}>
              <Input
                id="wf-name"
                type="text"
                placeholder="Workflow name..."
                autoComplete="off"
                invalid={!!errors.name}
                {...register("name")}
              />
            </FormField>

            <FormField id="wf-desc" label="Description" charCount={{ current: formDescription.length, max: 4000 }}>
              <Textarea
                id="wf-desc"
                placeholder="Describe what this workflow does..."
                rows={3}
                {...register("description")}
              />
            </FormField>

            {isError && <StatusMessage variant="error" message={mutationError?.message ?? "Unknown error"} />}

            <Button
              type="submit"
              variant="primary"
              fullWidth
              loading={isPending}
              disabled={!formName.trim()}
              icon={isSuccess && !isPending ? <CheckCircle size={16} /> : undefined}
            >
              {isPending ? "Saving..." : isSuccess ? "Saved" : selectedWorkflow ? "Save Changes" : "Create Workflow"}
            </Button>
          </div>
        </Card>

        <Card padding="none" as="section">
          <CardHeader
            icon={<Plus />}
            title="Add agents"
            actions={<span className={styles.availableCount}>{agents.length} available</span>}
          />
          {agents.length > 0 && (
            <div className={styles.pickerSearch}>
              <SearchInput
                placeholder="Search agents..."
                aria-label="Search agents"
                value={agentSearch}
                onChange={(e) => setAgentSearch(e.target.value)}
              />
            </div>
          )}
          <div className={styles.picker}>
            {agents.length === 0 ? (
              <span className={styles.pickerEmpty}>No agents registered yet</span>
            ) : filteredAgents.length === 0 ? (
              <span className={styles.pickerEmpty}>No agents match &ldquo;{agentSearch}&rdquo;</span>
            ) : (
              filteredAgents.map((agent) => (
                <button
                  key={agent.id}
                  type="button"
                  className={styles.pickerItem}
                  onClick={() => addAgentToForm(agent)}
                  aria-label={`Add ${agent.name}`}
                >
                  <span className={styles.pickerInfo}>
                    <span className={styles.pickerTop}>
                      <span className={styles.pickerName}>{agent.name}</span>
                      {agent.model && <span className={styles.pickerModel}>{agent.model}</span>}
                    </span>
                    {agent.description && <span className={styles.pickerDesc}>{agent.description}</span>}
                  </span>
                  <Plus size={16} className={styles.pickerAdd} aria-hidden="true" />
                </button>
              ))
            )}
          </div>
        </Card>
      </div>
    </form>
  );
}
