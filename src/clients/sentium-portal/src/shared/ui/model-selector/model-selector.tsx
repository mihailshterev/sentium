import { useEffect, useRef, useState } from "react";
import { Check, ChevronDown } from "lucide-react";
import { Input, Select } from "@/shared/ui/form-controls";
import styles from "./model-selector.module.css";

interface ModelSelectorProps {
  id?: string;
  className?: string;
  models: string[];
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
  /** `field`: a form control. `chip`: a compact pill with a dropdown, for toolbars. */
  variant?: "field" | "chip";
}

/**
 * Model picker. Falls back to free text when no models are known (e.g. the inference server is unreachable),
 * so a model name can always be entered.
 */
export function ModelSelector({
  id,
  className,
  models,
  value,
  onChange,
  placeholder,
  disabled,
  variant = "field",
}: ModelSelectorProps) {
  const [open, setOpen] = useState(false);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const sorted = [...models].sort((a, b) => a.localeCompare(b));

  useEffect(() => {
    if (!open) {
      return;
    }
    const onPointer = (e: MouseEvent) => {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("mousedown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const freeText = (inputClassName?: string) => (
    <Input
      id={id}
      className={inputClassName}
      controlSize={variant === "chip" ? "sm" : "md"}
      type="text"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder ?? "e.g. gemma-4-e4b"}
      autoComplete="off"
      spellCheck={false}
      disabled={disabled}
    />
  );

  if (variant === "field") {
    if (models.length === 0) {
      return freeText(className);
    }
    return (
      <Select
        id={id}
        className={className}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        disabled={disabled}
      >
        {sorted.map((m) => (
          <option key={m} value={m}>
            {m}
          </option>
        ))}
      </Select>
    );
  }

  if (models.length === 0) {
    return (
      <div className={[styles.wrapper, className ?? ""].filter(Boolean).join(" ")}>{freeText(styles.chipInput)}</div>
    );
  }

  return (
    <div ref={wrapperRef} className={[styles.wrapper, className ?? ""].filter(Boolean).join(" ")}>
      <button
        id={id}
        type="button"
        className={`${styles.trigger} ${open ? styles.triggerOpen : ""}`}
        onClick={() => setOpen((v) => !v)}
        onMouseDown={(e) => e.preventDefault()}
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={open}
      >
        {value ? (
          <span className={styles.triggerLabel}>{value}</span>
        ) : (
          <span className={styles.triggerPlaceholder}>{placeholder ?? "Select model"}</span>
        )}
        <ChevronDown size={13} className={`${styles.chevron} ${open ? styles.chevronOpen : ""}`} aria-hidden="true" />
      </button>
      {open && (
        <div className={styles.menu}>
          {sorted.map((m) => (
            <button
              key={m}
              type="button"
              className={`${styles.option} ${m === value ? styles.optionActive : ""}`}
              onMouseDown={(e) => e.preventDefault()}
              onClick={() => {
                onChange(m);
                setOpen(false);
              }}
            >
              <Check size={13} className={m === value ? styles.check : styles.checkHidden} aria-hidden="true" />
              <span>{m}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
