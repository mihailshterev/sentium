import { useState } from "react";
import { DndContext, closestCenter, type DragEndEvent, PointerSensor, useSensor, useSensors } from "@dnd-kit/core";
import { SortableContext, verticalListSortingStrategy, useSortable, arrayMove } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { Plus, X, GripVertical, Bot, GitBranch, Loader, CheckCircle, AlertCircle, Pencil, Trash2 } from "lucide-react";
import styles from "./workflows.module.scss";
import useWorkflows from "../../hooks/useWorkflows";
import useAgents from "../../hooks/useAgents";
import type { AgentRecord } from "../../types/agents";
import type { WorkflowRecord } from "../../types/workflows";
import type { SortableAgentItem } from "../../types/workflows";

const SortableAgent = ({ item, onRemove }: { item: SortableAgentItem; onRemove: (sortId: string) => void }) => {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: item.sortId });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
  };

  return (
    <div ref={setNodeRef} style={style} className={styles.sortableAgent}>
      <button type="button" className={styles.dragHandle} {...attributes} {...listeners}>
        <GripVertical size={14} />
      </button>
      <Bot size={13} className={styles.sortableAgentIcon} />
      <span className={styles.sortableAgentName}>{item.name}</span>
      <button className={styles.sortableAgentRemove} onClick={() => onRemove(item.sortId)}>
        <X size={12} />
      </button>
    </div>
  );
};

const Workflows = () => {
  const {
    workflows,
    isLoading,
    createWorkflow,
    isCreatingWorkflow,
    isCreateSuccess,
    isCreateError,
    createWorkflowError,
    resetCreate,
    updateWorkflow,
    isUpdatingWorkflow,
    isUpdateSuccess,
    isUpdateError,
    updateWorkflowError,
    resetUpdate,
    deleteWorkflow,
  } = useWorkflows();
  const { agents } = useAgents();

  const [selectedWorkflow, setSelectedWorkflow] = useState<WorkflowRecord | null>(null);
  const [isEditing, setIsEditing] = useState(false);

  const [formName, setFormName] = useState("");
  const [formDescription, setFormDescription] = useState("");
  const [formAgents, setFormAgents] = useState<SortableAgentItem[]>([]);

  const sensors = useSensors(useSensor(PointerSensor));

  const activeMutation = {
    isPending: selectedWorkflow ? isUpdatingWorkflow : isCreatingWorkflow,
    isSuccess: selectedWorkflow ? isUpdateSuccess : isCreateSuccess,
    isError: selectedWorkflow ? isUpdateError : isCreateError,
    error: selectedWorkflow ? updateWorkflowError : createWorkflowError,
  };

  const resetMutations = () => {
    resetCreate();
    resetUpdate();
  };

  const openCreate = () => {
    setSelectedWorkflow(null);
    setIsEditing(true);
    setFormName("");
    setFormDescription("");
    setFormAgents([]);
    resetMutations();
  };

  const openEdit = (workflow: WorkflowRecord) => {
    setSelectedWorkflow(workflow);
    setIsEditing(true);
    setFormName(workflow.name);
    setFormDescription(workflow.description);
    setFormAgents(
      workflow.agents.map((a) => ({
        sortId: `${a.agentId}-${a.order}`,
        agentId: a.agentId,
        name: agents.find((ag) => ag.id === a.agentId)?.name ?? a.agentId.slice(0, 8),
      })),
    );
    resetMutations();
  };

  const closeEdit = () => {
    setIsEditing(false);
    setSelectedWorkflow(null);
    resetMutations();
  };

  const addAgentToForm = (agent: AgentRecord) => {
    setFormAgents((prev) => [
      ...prev,
      {
        sortId: `${agent.id}-${Date.now()}`,
        agentId: agent.id,
        name: agent.name,
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

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const payload = {
      name: formName.trim(),
      description: formDescription.trim(),
      agents: formAgents.map((a, idx) => ({ agentId: a.agentId, order: idx })),
    };

    if (selectedWorkflow) {
      updateWorkflow({ id: selectedWorkflow.id, ...payload }, { onSuccess: () => setTimeout(() => closeEdit(), 900) });
    } else {
      createWorkflow(payload, {
        onSuccess: () => setTimeout(() => closeEdit(), 900),
      });
    }
  };

  const handleDelete = (workflowId: string) => {
    if (!confirm("Delete this workflow?")) return;
    deleteWorkflow(workflowId, {
      onSuccess: () => {
        if (selectedWorkflow?.id === workflowId) closeEdit();
      },
    });
  };

  const getAgentName = (agentId: string) => agents.find((a) => a.id === agentId)?.name ?? agentId.slice(0, 8);

  return (
    <div className={styles.container}>
      <div className={styles.pageHeader}>
        <div className={styles.headerLeft}>
          <GitBranch size={20} className={styles.headerIcon} />
          <div>
            <h2 className={styles.headerTitle}>Workflow Builder</h2>
            <span className={styles.headerSub}>Compose agents into ordered execution pipelines</span>
          </div>
        </div>
        <div className={styles.headerRight}>
          <div className={styles.headerBadge}>
            <GitBranch size={13} />
            <span>{workflows.length} workflows</span>
          </div>
          <button className={styles.createBtn} onClick={openCreate}>
            <Plus size={14} />
            New Workflow
          </button>
        </div>
      </div>

      <div className={styles.body}>
        <aside className={styles.workflowList}>
          <div className={styles.listHeader}>
            <span className={styles.activeDot} />
            <span>Defined Workflows</span>
          </div>

          <div className={styles.listScroll}>
            {isLoading && (
              <div className={styles.listPlaceholder}>
                <Loader size={18} className={styles.spinIcon} />
                <span>Loading...</span>
              </div>
            )}
            {!isLoading && workflows.length === 0 && (
              <div className={styles.listPlaceholder}>
                <GitBranch size={28} className={styles.emptyIcon} />
                <span>No workflows yet</span>
              </div>
            )}
            {workflows.map((wf) => (
              <div
                key={wf.id}
                className={`${styles.workflowCard} ${selectedWorkflow?.id === wf.id ? styles.workflowCardActive : ""}`}
                onClick={() => openEdit(wf)}
              >
                <div className={styles.workflowCardHeader}>
                  <GitBranch size={13} className={styles.wfIcon} />
                  <span className={styles.wfName}>{wf.name}</span>
                </div>
                <p className={styles.wfDescription}>{wf.description || "No description"}</p>
                <div className={styles.wfMeta}>
                  <span className={styles.wfAgentCount}>
                    <Bot size={11} />
                    {wf.agents.length} agent{wf.agents.length !== 1 ? "s" : ""}
                  </span>
                  <div className={styles.wfActions}>
                    <button
                      className={styles.wfActionBtn}
                      onClick={(e) => {
                        e.stopPropagation();
                        openEdit(wf);
                      }}
                      title="Edit"
                    >
                      <Pencil size={11} />
                    </button>
                    <button
                      className={`${styles.wfActionBtn} ${styles.wfActionBtnDanger}`}
                      onClick={(e) => {
                        e.stopPropagation();
                        handleDelete(wf.id);
                      }}
                      title="Delete"
                    >
                      <Trash2 size={11} />
                    </button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </aside>

        <main className={styles.editorPanel}>
          {!isEditing ? (
            <div className={styles.editorEmpty}>
              <GitBranch size={36} className={styles.editorEmptyIcon} />
              <p>Select a workflow to edit or create a new one</p>
              <button className={styles.createBtn} onClick={openCreate}>
                <Plus size={14} />
                New Workflow
              </button>
            </div>
          ) : (
            <div className={styles.editorContent}>
              <div className={styles.editorHeader}>
                <span className={styles.editorTitle}>{selectedWorkflow ? "Edit Workflow" : "New Workflow"}</span>
                <button className={styles.closeBtn} onClick={closeEdit}>
                  <X size={14} />
                </button>
              </div>

              <form onSubmit={handleSubmit} className={styles.editorForm}>
                <div className={styles.editorColumns}>
                  <div className={styles.formColumn}>
                    <div className={styles.fieldGroup}>
                      <label className={styles.fieldLabel} htmlFor="wf-name">
                        Name
                      </label>
                      <input
                        id="wf-name"
                        className={styles.fieldInput}
                        type="text"
                        value={formName}
                        onChange={(e) => setFormName(e.target.value)}
                        placeholder="e.g. Threat Response Pipeline"
                        maxLength={255}
                        required
                        autoComplete="off"
                      />
                    </div>

                    <div className={styles.fieldGroup}>
                      <label className={styles.fieldLabel} htmlFor="wf-desc">
                        Description
                        <span className={styles.charCount}>{formDescription.length}/4000</span>
                      </label>
                      <textarea
                        id="wf-desc"
                        className={`${styles.fieldInput} ${styles.fieldTextarea}`}
                        value={formDescription}
                        onChange={(e) => setFormDescription(e.target.value)}
                        placeholder="Describe what this workflow does..."
                        maxLength={4000}
                        rows={4}
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
                      className={`${styles.submitBtn} ${activeMutation.isPending ? styles.submitting : ""}`}
                      disabled={activeMutation.isPending || !formName.trim()}
                    >
                      {activeMutation.isPending ? (
                        <>
                          <Loader size={14} className={styles.spinIcon} /> Saving...
                        </>
                      ) : activeMutation.isSuccess ? (
                        <>
                          <CheckCircle size={14} /> Saved
                        </>
                      ) : selectedWorkflow ? (
                        "Save Changes"
                      ) : (
                        "Create Workflow"
                      )}
                    </button>

                    {activeMutation.isError && (
                      <div className={styles.errorMsg}>
                        <AlertCircle size={13} />
                        {activeMutation.error?.message ?? "Unknown error"}
                      </div>
                    )}
                  </div>

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
                              <div key={item.sortId} className={styles.sortableRow}>
                                <span className={styles.sortableIndex}>{String(idx + 1).padStart(2, "0")}</span>
                                <SortableAgent item={item} onRemove={removeAgentFromForm} />
                              </div>
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
                </div>
              </form>
            </div>
          )}
        </main>
      </div>
    </div>
  );
};

export default Workflows;
