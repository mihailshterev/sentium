import { useRef, useState } from "react";
import { X } from "lucide-react";
import { unlockAudio } from "../audio/audio-context";
import { speechPlayer } from "../audio/speech-player";
import { useCompanionSettings } from "../hooks/useCompanionSettings";
import { describeState } from "../state/companion-machine";
import { useCompanionStore } from "../state/companion-store";
import { CompanionRig } from "./companion-rig";
import { QuickChat } from "./quick-chat";
import styles from "./companion.module.css";

const PANEL_GAP = 8;
const DISMISSED_KEY = "sentium-companion-dismissed";

const wasDismissed = () => {
  try {
    return sessionStorage.getItem(DISMISSED_KEY) === "1";
  } catch {
    return false;
  }
};

interface CompanionProps {
  /** `sidebar`: its own box in the navigation. `floating`: a dismissible bubble for phone-width screens. */
  placement: "sidebar" | "floating";
  /** Sidebar only: the rail shows just the head. */
  collapsed?: boolean;
}

/**
 * The Companion: the animated character that stands for the assistant. It shows what the assistant is doing and
 * opens the quick chat. It is a presentation of the assistant, not a separate agent.
 */
export function Companion({ placement, collapsed = false }: CompanionProps) {
  const { isCompanionEnabled } = useCompanionSettings();
  const state = useCompanionStore((s) => s.state);
  const [panelPosition, setPanelPosition] = useState<React.CSSProperties | null>(null);
  const [dismissed, setDismissed] = useState(wasDismissed);
  const anchorRef = useRef<HTMLButtonElement>(null);

  const floating = placement === "floating";
  if (!isCompanionEnabled || (floating && dismissed)) {
    return null;
  }

  const open = panelPosition !== null;

  const toggle = () => {
    // Clicking the Companion always silences it.
    speechPlayer.stop();
    unlockAudio();

    if (open) {
      setPanelPosition(null);
      return;
    }

    const rect = anchorRef.current?.getBoundingClientRect();
    if (!rect) {
      setPanelPosition({});
    } else if (floating) {
      setPanelPosition({ right: PANEL_GAP, bottom: window.innerHeight - rect.top + PANEL_GAP });
    } else {
      setPanelPosition({ left: rect.right + PANEL_GAP, bottom: Math.max(PANEL_GAP, window.innerHeight - rect.bottom) });
    }
  };

  const dismiss = () => {
    try {
      sessionStorage.setItem(DISMISSED_KEY, "1");
    } catch {
      // Private mode: dismissed for this page view only.
    }
    setPanelPosition(null);
    setDismissed(true);
  };

  const description = describeState(state);

  return (
    <div className={styles.dock} data-placement={placement} data-collapsed={collapsed || undefined}>
      <button
        ref={anchorRef}
        type="button"
        className={styles.companion}
        data-testid="companion"
        data-state={state}
        data-label={description}
        aria-label={`Companion, ${description}. Open quick chat`}
        aria-haspopup="dialog"
        aria-expanded={open}
        title={collapsed || floating ? "Companion" : undefined}
        onClick={toggle}
      >
        <CompanionRig state={state} compact={collapsed || floating} className={styles.rig} />
      </button>

      {floating && (
        <button type="button" className={styles.dismiss} aria-label="Hide the Companion" onClick={dismiss}>
          <X size={12} aria-hidden="true" />
        </button>
      )}

      {panelPosition && (
        <QuickChat anchor={anchorRef} position={panelPosition} onClose={() => setPanelPosition(null)} />
      )}
    </div>
  );
}
