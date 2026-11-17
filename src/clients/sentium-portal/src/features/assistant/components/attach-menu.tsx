import { useCallback, useRef, useState } from "react";
import { Paperclip } from "lucide-react";
import { FilePickerPopover } from "@/features/workspaces/components/file-picker-popover";
import type { WorkspaceFile } from "@/features/workspaces/types/workspace";
import styles from "../assistant.module.css";

interface AttachMenuProps {
  preferredWorkspaceId: string | null;
  selectedIds: readonly string[];
  onToggleFile: (file: WorkspaceFile) => void;
  onUploadFiles: (files: File[]) => void;
  /** Accepted extensions for the file chooser. */
  accept?: string;
  disabled?: boolean;
}

/** The composer's paperclip: attach workspace files, or upload new ones from the computer. */
export function AttachMenu({ preferredWorkspaceId, selectedIds, onToggleFile, onUploadFiles, accept, disabled }: AttachMenuProps) {
  const [open, setOpen] = useState(false);
  const anchorRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const close = useCallback(() => setOpen(false), []);

  const chooseFromComputer = () => {
    setOpen(false);
    inputRef.current?.click();
  };

  return (
    <div ref={anchorRef} className={styles.attachMenu}>
      <button
        type="button"
        className={styles.attachButton}
        onClick={() => setOpen((v) => !v)}
        disabled={disabled}
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-label="Attach files"
        title="Attach files"
      >
        <Paperclip size={16} />
      </button>
      <input
        ref={inputRef}
        type="file"
        multiple
        accept={accept}
        hidden
        data-testid="composer-file-input"
        onChange={(e) => {
          const files = Array.from(e.target.files ?? []);
          e.target.value = "";
          if (files.length > 0) {
            onUploadFiles(files);
          }
        }}
      />
      <FilePickerPopover
        open={open}
        onClose={close}
        anchorRef={anchorRef}
        preferredWorkspaceId={preferredWorkspaceId}
        selectedIds={selectedIds}
        onToggleFile={onToggleFile}
        onUploadClick={chooseFromComputer}
        className={styles.attachPopover}
      />
    </div>
  );
}
