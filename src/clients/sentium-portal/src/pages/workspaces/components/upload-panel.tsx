import { UploadCloud, CheckCircle, AlertCircle, Loader2 } from "lucide-react";
import styles from "../workspaces.module.scss";
import { formatBytesToMb } from "../../../utils/formatters";

interface UploadPanelProps {
  workspaceName: string;
  selectedFile: File | null;
  dragActive: boolean;
  fileTypeError: string | null;
  isUploading: boolean;
  isUploadSuccess: boolean;
  isUploadError: boolean;
  uploadError: Error | null;
  acceptedExtensions: string;
  onDrop: (e: React.DragEvent) => void;
  onDragOver: (e: React.DragEvent) => void;
  onDragLeave: () => void;
  onInputChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
  onSubmit: (e: React.FormEvent) => void;
  fileInputRef: React.RefObject<HTMLInputElement | null>;
}

const UploadPanel = ({
  workspaceName,
  selectedFile,
  dragActive,
  fileTypeError,
  isUploading,
  isUploadSuccess,
  isUploadError,
  uploadError,
  acceptedExtensions,
  onDrop,
  onDragOver,
  onDragLeave,
  onInputChange,
  onSubmit,
  fileInputRef,
}: UploadPanelProps) => (
  <form className={styles.uploadPanel} onSubmit={onSubmit}>
    <p className={styles.panelTitle}>Upload File</p>
    <div
      className={`${styles.uploadArea} ${dragActive ? styles.uploadAreaActive : ""}`}
      onClick={() => fileInputRef.current?.click()}
      onDrop={onDrop}
      onDragOver={onDragOver}
      onDragLeave={onDragLeave}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => e.key === "Enter" && fileInputRef.current?.click()}
    >
      <UploadCloud size={28} className={styles.uploadIcon} />
      {selectedFile ? (
        <>
          <span className={styles.uploadText}>{selectedFile.name}</span>
          <span className={styles.uploadHint}>{formatBytesToMb(selectedFile.size)}</span>
        </>
      ) : (
        <>
          <span className={styles.uploadText}>Drop a file or click to browse</span>
          <span className={styles.uploadHint}>Text files up to 100 MB — .txt .md .json .csv .yaml .py .ts …</span>
        </>
      )}
    </div>
    <input
      ref={fileInputRef}
      type="file"
      className={styles.fileInput}
      onChange={onInputChange}
      accept={acceptedExtensions}
    />

    {fileTypeError && (
      <div className={`${styles.statusBox} ${styles["status-error"]}`}>
        <AlertCircle size={14} className={styles.statusIcon} />
        <span className={styles.statusMessage}>{fileTypeError}</span>
      </div>
    )}

    {isUploading && (
      <div className={`${styles.statusBox} ${styles["status-loading"]}`}>
        <Loader2 size={14} className={`${styles.statusIcon} ${styles.spin}`} />
        <span className={styles.statusMessage}>Uploading…</span>
      </div>
    )}

    {isUploadSuccess && (
      <div className={`${styles.statusBox} ${styles["status-success"]}`}>
        <CheckCircle size={14} className={styles.statusIcon} />
        <span className={styles.statusMessage}>File uploaded — indexing in background.</span>
      </div>
    )}

    {isUploadError && (
      <div className={`${styles.statusBox} ${styles["status-error"]}`}>
        <AlertCircle size={14} className={styles.statusIcon} />
        <span className={styles.statusMessage}>
          {uploadError instanceof Error ? uploadError.message : "Upload failed."}
        </span>
      </div>
    )}

    <button type="submit" className={styles.submitButton} disabled={!selectedFile || isUploading}>
      {isUploading ? <Loader2 size={14} className={styles.spin} /> : <UploadCloud size={14} />}
      Upload to {workspaceName}
    </button>
  </form>
);

export default UploadPanel;
