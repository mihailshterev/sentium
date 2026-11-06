import { Download, ExternalLink, File } from "lucide-react";
import styles from "../sandbox.module.css";
import { buttonClass } from "@/shared/ui/button";
import { getArtifactUrl } from "../api/sandbox.service";
import { formatBytesToMb } from "../../../shared/utils/formatters";
import type { ArtifactDto } from "@/features/sandbox/types/sandbox";

function isImageMime(mime: string) {
  return mime.startsWith("image/");
}

interface ArtifactCardProps {
  artifact: ArtifactDto;
}

export function ArtifactCard({ artifact }: ArtifactCardProps) {
  const isImage = isImageMime(artifact.mimeType);
  const shortName = artifact.fileName.split("/").pop() ?? artifact.fileName;
  const url = getArtifactUrl(artifact.downloadPath);

  return (
    <div className={styles.artifact}>
      {isImage ? (
        <a href={url} target="_blank" rel="noopener noreferrer" className={styles.artifactPreview}>
          <img
            src={url}
            alt={shortName}
            className={styles.artifactThumb}
            onError={(e) => ((e.currentTarget as HTMLImageElement).style.display = "none")}
          />
        </a>
      ) : (
        <div className={styles.artifactPreview} aria-hidden="true">
          <File size={24} />
        </div>
      )}
      <div className={styles.artifactBody}>
        <span className={styles.artifactName} title={artifact.fileName}>
          {shortName}
        </span>
        <span className={styles.artifactMeta}>
          {artifact.mimeType} · {formatBytesToMb(artifact.sizeBytes)}
        </span>
      </div>
      <div className={styles.artifactActions}>
        <a
          href={url}
          download={shortName}
          rel="noopener noreferrer"
          className={buttonClass({ variant: "secondary", size: "sm", fullWidth: true })}
        >
          <Download size={14} aria-hidden="true" /> Download
        </a>
        {isImage && (
          <a
            href={url}
            target="_blank"
            rel="noopener noreferrer"
            className={buttonClass({ variant: "ghost", size: "sm", iconOnly: true })}
            aria-label={`Open ${shortName}`}
          >
            <ExternalLink size={14} />
          </a>
        )}
      </div>
    </div>
  );
}
