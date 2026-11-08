import { useState } from "react";
import { Wrench } from "lucide-react";
import styles from "../agents.module.css";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { StatusMessage } from "@/shared/ui/status-message";
import type { BuiltInAgent, ToolDescriptor } from "../types/agents";
import { describeTools } from "../utils/describe-tools";
import { ToolPicker } from "./tool-picker";

interface BuiltInToolsDialogProps {
  agent: BuiltInAgent;
  tools: ToolDescriptor[];
  isSaving: boolean;
  isResetting: boolean;
  error: Error | null;
  onSave: (tools: string[] | null) => void;
  onReset: () => void;
  onClose: () => void;
}

export function BuiltInToolsDialog({
  agent,
  tools,
  isSaving,
  isResetting,
  error,
  onSave,
  onReset,
  onClose,
}: BuiltInToolsDialogProps) {
  const [value, setValue] = useState<string[] | null>(agent.tools);

  return (
    <Dialog
      open
      onClose={onClose}
      title={`${agent.name} tools`}
      description={`Default: ${describeTools(agent.defaultTools)}. Your choice applies to your chats and workflow runs only.`}
      icon={<Wrench />}
    >
      <div className={styles.form}>
        <ToolPicker id="built-in-tools" tools={tools} value={value} onChange={setValue} />

        {error && <StatusMessage variant="error" message={error.message || "Could not save the tools."} />}

        <div className={styles.dialogActions}>
          {agent.isCustomized && (
            <Button variant="ghost" onClick={onReset} loading={isResetting} className={styles.resetButton}>
              Reset to default
            </Button>
          )}
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" onClick={() => onSave(value)} loading={isSaving}>
            Save tools
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
