import { Download, ExternalLink, File } from "lucide-react";
import styles from "../sandbox.module.scss";
import { getArtifactUrl } from "../../../services/sandbox.service";
import { formatBytesToMb } from "../../../utils/formatters";
import type { ArtifactDto } from "../../../types/sandbox";

function isImageMime(mime: string) {
  return mime.startsWith("image/");
}

interface ArtifactCardProps {
  artifact: ArtifactDto;
}

const ArtifactCard = ({ artifact }: ArtifactCardProps) => {
  const isImage = isImageMime(artifact.mimeType);
  const shortName = artifact.fileName.split("/").pop() ?? artifact.fileName;
  const url = getArtifactUrl(artifact.downloadPath);

  return (
    <div className={styles.artifactCard}>
      {isImage ? (
        <a href={url} target="_blank" rel="noopener noreferrer">
          <img
            src={url}
            alt={shortName}
            className={styles.artifactThumb}
            onError={(e) => ((e.currentTarget as HTMLImageElement).style.display = "none")}
          />
        </a>
      ) : (
        <div className={styles.artifactIconWrap}>
          <File size={26} />
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
        <a href={url} download={shortName} className={styles.artifactDownloadBtn} rel="noopener noreferrer">
          <Download size={11} /> Download
        </a>
        {isImage && (
          <a href={url} target="_blank" rel="noopener noreferrer" className={styles.artifactViewBtn}>
            <ExternalLink size={11} />
          </a>
        )}
      </div>
    </div>
  );
};

export default ArtifactCard;
