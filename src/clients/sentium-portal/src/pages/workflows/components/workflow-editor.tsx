import { Fragment, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { DndContext, closestCenter, type DragEndEvent, PointerSensor, useSensor, useSensors } from "@dnd-kit/core";
import { SortableContext, verticalListSortingStrategy, arrayMove } from "@dnd-kit/sortable";
import { Plus, X, Bot, Loader, CheckCircle, AlertCircle } from "lucide-react";
import styles from "../workflows.module.scss";
import StatusMessage from "../../../components/ui/status-message";
import SortableAgent from "./sortable-agent";
import type { AgentRecord } from "../../../types/agents";
import type { WorkflowRecord, SortableAgentItem } from "../../../types/workflows";
import { workflowEditorSchema, type WorkflowEditorFormData } from "../../../schemas/workflow.editor";

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
  onClose: () => void;
  onSubmit: (data: { name: string; description: string; agents: { agentId: string; order: number }[] }) => void;
  getAgentName: (agentId: string) => string;
  initialFormAgents: SortableAgentItem[];
  initialName: string;
  initialDescription: string;
}

const WorkflowEditor = ({
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
  onClose,
  onSubmit,
  getAgentName,
  initialFormAgents,
  initialName,
  initialDescription,
}: WorkflowEditorProps) => {
  const [formAgents, setFormAgents] = useState<SortableAgentItem[]>(initialFormAgents);

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
    <div className={styles.editorContent}>
      <div className={styles.editorHeader}>
        <span className={styles.editorTitle}>{selectedWorkflow ? "Edit Workflow" : "New Workflow"}</span>
        <button className={styles.closeBtn} onClick={onClose} title="Close">
          <X size={14} />
        </button>
      </div>

      <form onSubmit={handleSubmit(handleFormSubmit)} className={styles.editorForm}>
        <div className={styles.editorColumns}>
          <div className={styles.pipelineColumn}>
            <div className={styles.pipelineHeader}>
              <span>Execution Order</span>
              <span className={styles.pipelineCount}>{formAgents.length} agents</span>
            </div>

            {formAgents.length === 0 ? (
              <div className={styles.pipelineEmpty}>
                <Bot size={24} className={styles.pipelineEmptyIcon} />
                <span>Add agents from the list</span>
                <span className={styles.pipelineEmptyHint}>Drag to reorder execution sequence</span>
              </div>
            ) : (
              <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
                <SortableContext items={formAgents.map((a) => a.sortId)} strategy={verticalListSortingStrategy}>
                  <div className={styles.sortableList}>
                    {formAgents.map((item, idx) => (
                      <Fragment key={item.sortId}>
                        {idx > 0 && (
                          <div className={styles.nodeConnector}>
                            <div className={styles.connectorLine}>
                              <div className={styles.connectorFlow} style={{ animationDelay: `${idx * 0.35}s` }} />
                            </div>
                            <div className={styles.connectorArrow} />
                          </div>
                        )}
                        <div className={styles.sortableRow}>
                          <span className={styles.sortableIndex}>{String(idx + 1).padStart(2, "0")}</span>
                          <SortableAgent item={item} onRemove={removeAgentFromForm} />
                        </div>
                      </Fragment>
                    ))}
                  </div>
                </SortableContext>
              </DndContext>
            )}

            {selectedWorkflow && formAgents.length === 0 && (
              <div className={styles.wfAgentPreview}>
                {selectedWorkflow.agents
                  .sort((a, b) => a.order - b.order)
                  .map((a, idx) => (
                    <div key={a.agentId} className={styles.previewAgent}>
                      <span className={styles.previewIndex}>{idx + 1}</span>
                      <Bot size={12} />
                      <span>{getAgentName(a.agentId)}</span>
                    </div>
                  ))}
              </div>
            )}
          </div>

          <div className={styles.formColumn}>
            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="wf-name">
                Name
              </label>
              <input
                id="wf-name"
                className={styles.fieldInput}
                type="text"
                placeholder="e.g. Local AI Inference Pipeline"
                autoComplete="off"
                {...register("name")}
              />
              {errors.name && <StatusMessage variant="error" message={errors.name.message ?? "Invalid name"} />}
            </div>

            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="wf-desc">
                Description
                <span className={styles.charCount}>{formDescription.length}/4000</span>
              </label>
              <textarea
                id="wf-desc"
                className={`${styles.fieldInput} ${styles.fieldTextarea}`}
                placeholder="Describe what this workflow does..."
                rows={4}
                {...register("description")}
              />
            </div>

            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel}>Add Agents</label>
              <div className={styles.agentPicker}>
                {agents.length === 0 && <span className={styles.pickerEmpty}>No agents registered yet</span>}
                {agents.map((agent) => (
                  <button
                    key={agent.id}
                    type="button"
                    className={styles.agentPickerItem}
                    onClick={() => addAgentToForm(agent)}
                  >
                    <Bot size={12} />
                    <span>{agent.name}</span>
                    <Plus size={11} className={styles.addIcon} />
                  </button>
                ))}
              </div>
            </div>

            <button
              type="submit"
              className={`${styles.submitBtn} ${isPending ? styles.submitting : ""}`}
              disabled={isPending || !formName.trim()}
            >
              {isPending ? (
                <>
                  <Loader size={14} className={styles.spinIcon} /> Saving...
                </>
              ) : isSuccess ? (
                <>
                  <CheckCircle size={14} /> Saved
                </>
              ) : selectedWorkflow ? (
                "Save Changes"
              ) : (
                "Create Workflow"
              )}
            </button>

            {isError && (
              <div className={styles.errorMsg}>
                <AlertCircle size={13} />
                {mutationError?.message ?? "Unknown error"}
              </div>
            )}
          </div>
        </div>
      </form>
    </div>
  );
};

export default WorkflowEditor;
