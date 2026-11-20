import { useRef } from "react";
import { Camera, Trash2 } from "lucide-react";
import styles from "../profile.module.css";
import { Avatar } from "@/shared/ui/avatar";
import { Button } from "@/shared/ui/button";

interface AvatarPickerProps {
  name: string;
  avatarUrl: string | null;
  isUploading: boolean;
  isRemoving: boolean;
  onPick: (file: File) => void;
  onRemove: () => void;
}

/** The profile's large avatar plus the controls to replace or remove the picture. */
export function AvatarPicker({ name, avatarUrl, isUploading, isRemoving, onPick, onRemove }: AvatarPickerProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const isBusy = isUploading || isRemoving;

  const handleChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    // Clear the input so choosing the same file again still fires a change.
    event.target.value = "";
    if (file) {
      onPick(file);
    }
  };

  return (
    <div className={styles.picker}>
      <button
        type="button"
        className={styles.pickerButton}
        onClick={() => inputRef.current?.click()}
        disabled={isBusy}
        aria-label="Change profile picture"
        aria-busy={isUploading || undefined}
      >
        <Avatar name={name} src={avatarUrl} size="lg" />
        <span className={styles.pickerBadge} aria-hidden="true">
          <Camera size={12} />
        </span>
      </button>
      <input
        ref={inputRef}
        type="file"
        accept="image/png,image/jpeg,image/webp,image/gif,image/avif,image/heic"
        className={styles.pickerInput}
        onChange={handleChange}
        tabIndex={-1}
        aria-hidden="true"
        data-testid="avatar-input"
      />
      {avatarUrl && (
        <Button
          variant="ghost"
          size="sm"
          icon={<Trash2 size={13} />}
          loading={isRemoving}
          disabled={isBusy}
          onClick={onRemove}
          className={styles.pickerRemove}
        >
          Remove
        </Button>
      )}
    </div>
  );
}
