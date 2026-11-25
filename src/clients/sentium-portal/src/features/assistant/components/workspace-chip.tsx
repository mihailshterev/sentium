import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Check, ChevronDown, FolderOpen } from "lucide-react";
import { workspaceQueries } from "@/features/workspaces/api/workspaces.queries";
import chip from "@/shared/ui/model-selector/model-selector.module.css";
import styles from "../assistant.module.css";

export interface ChatWorkspace {
  id: string;
  name: string;
}

interface WorkspaceChipProps {
  value: ChatWorkspace | null;
  onChange: (workspace: ChatWorkspace | null) => void;
  disabled?: boolean;
}

/**
 * The conversation's workspace: every turn can draw on its files, and uploads from the composer land in it. Shares
 * the model selector's chip look so the composer bar reads as one row of settings.
 */
export function WorkspaceChip({ value, onChange, disabled }: WorkspaceChipProps) {
  const [open, setOpen] = useState(false);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const { data: workspaces = [], isLoading, isError } = useQuery({
    ...workspaceQueries.options(),
    enabled: open,
  });

  useEffect(() => {
    if (!open) {
      return;
    }
    const onPointer = (e: PointerEvent) => {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("pointerdown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const choose = (workspace: ChatWorkspace | null) => {
    onChange(workspace);
    setOpen(false);
  };

  return (
    <div ref={wrapperRef} className={chip.wrapper}>
      <button
        type="button"
        className={`${chip.trigger} ${open ? chip.triggerOpen : ""} ${styles.workspaceChip}`}
        onClick={() => setOpen((v) => !v)}
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-label={value ? `Workspace: ${value.name}` : "Choose a workspace"}
        title={value ? `Workspace: ${value.name}` : "No workspace - choose one so the assistant can use its files"}
      >
        <FolderOpen size={13} aria-hidden="true" />
        <span className={value ? chip.triggerLabel : chip.triggerPlaceholder}>{value?.name ?? "No workspace"}</span>
        <ChevronDown size={13} className={`${chip.chevron} ${open ? chip.chevronOpen : ""}`} aria-hidden="true" />
      </button>
      {open && (
        <div className={chip.menu} role="listbox" aria-label="Workspaces">
          <WorkspaceOption label="No workspace" active={value === null} onSelect={() => choose(null)} />
          {isLoading && <p className={styles.workspaceChipNote}>Loading…</p>}
          {isError && <p className={styles.workspaceChipNote}>Workspaces could not be loaded.</p>}
          {workspaces.map((ws) => (
            <WorkspaceOption
              key={ws.id}
              label={ws.name}
              hint={`${ws.fileCount} file${ws.fileCount === 1 ? "" : "s"}`}
              active={value?.id === ws.id}
              onSelect={() => choose({ id: ws.id, name: ws.name })}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function WorkspaceOption({
  label,
  hint,
  active,
  onSelect,
}: {
  label: string;
  hint?: string;
  active: boolean;
  onSelect: () => void;
}) {
  return (
    <button
      type="button"
      role="option"
      aria-selected={active}
      className={`${chip.option} ${active ? chip.optionActive : ""}`}
      onClick={onSelect}
    >
      <Check size={13} className={active ? chip.check : chip.checkHidden} aria-hidden="true" />
      <span>{label}</span>
      {hint && <span className={styles.workspaceChipNote}>{hint}</span>}
    </button>
  );
}
