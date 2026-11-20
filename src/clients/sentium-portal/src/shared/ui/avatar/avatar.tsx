import { useState } from "react";
import styles from "./avatar.module.css";
import { initialsOf } from "./initials";

export type AvatarSize = "sm" | "md" | "lg";

interface AvatarProps {
  /** Display name the initials are taken from when there is no picture. */
  name: string;
  src?: string | null;
  size?: AvatarSize;
  className?: string;
}

/** A user's profile picture, falling back to their initials when they have none or it fails to load. */
export function Avatar({ name, src, size = "md", className }: AvatarProps) {
  const [failedSrc, setFailedSrc] = useState<string | null>(null);
  const showPicture = !!src && src !== failedSrc;

  return (
    <span
      className={[styles.avatar, styles[size], className].filter(Boolean).join(" ")}
      data-has-picture={showPicture || undefined}
      aria-hidden="true"
    >
      {showPicture ? (
        <img className={styles.picture} src={src} alt="" decoding="async" onError={() => setFailedSrc(src)} />
      ) : (
        initialsOf(name)
      )}
    </span>
  );
}
