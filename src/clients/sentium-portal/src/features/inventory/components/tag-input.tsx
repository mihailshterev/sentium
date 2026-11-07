import { useId, useState } from "react";
import { X } from "lucide-react";
import styles from "../inventory.module.css";
import { normalizeTag } from "../schemas/inventory.form";

interface TagInputProps {
  id?: string;
  value: string[];
  onChange: (tags: string[]) => void;
  suggestions?: string[];
  placeholder?: string;
  maxTags?: number;
  maxLength?: number;
  invalid?: boolean;
  "aria-label"?: string;
}

export function TagInput({
  id,
  value,
  onChange,
  suggestions = [],
  placeholder = "Add a tag…",
  maxTags = 20,
  maxLength = 32,
  invalid,
  "aria-label": ariaLabel,
}: TagInputProps) {
  const listId = useId();
  const [draft, setDraft] = useState("");

  const add = (raw: string) => {
    const tag = normalizeTag(raw.replace(/,/g, " "));
    setDraft("");
    if (!tag || value.includes(tag) || value.length >= maxTags) return;
    onChange([...value, tag.slice(0, maxLength)]);
  };

  const remove = (tag: string) => onChange(value.filter((t) => t !== tag));

  const available = suggestions.filter((s) => !value.includes(s));

  return (
    <div className={styles.tagInput} data-invalid={invalid || undefined}>
      {value.map((tag) => (
        <span key={tag} className={styles.tagChip}>
          {tag}
          <button
            type="button"
            className={styles.tagRemove}
            aria-label={`Remove tag ${tag}`}
            onClick={() => remove(tag)}
          >
            <X size={12} />
          </button>
        </span>
      ))}
      <input
        id={id}
        className={styles.tagField}
        value={draft}
        list={available.length > 0 ? listId : undefined}
        placeholder={value.length >= maxTags ? "Tag limit reached" : placeholder}
        disabled={value.length >= maxTags}
        aria-label={ariaLabel}
        maxLength={maxLength + 1}
        onChange={(e) => {
          const next = e.target.value;
          if (next.endsWith(",")) add(next.slice(0, -1));
          else setDraft(next);
        }}
        onKeyDown={(e) => {
          if (e.key === "Enter") {
            e.preventDefault();
            if (draft.trim()) add(draft);
          } else if (e.key === "Backspace" && draft === "" && value.length > 0) {
            remove(value[value.length - 1]);
          }
        }}
        onBlur={() => draft.trim() && add(draft)}
      />
      {available.length > 0 && (
        <datalist id={listId}>
          {available.map((s) => (
            <option key={s} value={s} />
          ))}
        </datalist>
      )}
    </div>
  );
}
