import { Loader2, Mic } from "lucide-react";
import { beginPushToTalk, endPushToTalk } from "../audio/push-to-talk";
import { useCompanionStore } from "../state/companion-store";
import styles from "./push-to-talk-button.module.css";

interface PushToTalkButtonProps {
  /** Called with the transcript once the button is released and something was said. */
  onTranscript: (text: string) => void;
  disabled?: boolean;
  className?: string;
}

const isHoldKey = (event: React.KeyboardEvent) => event.key === " " || event.key === "Enter";

/**
 * Hold to talk: the microphone is open exactly while the button (or Space/Enter on it) is held. Renders nothing
 * when voice is off or unavailable, so pages need no checks of their own.
 */
export function PushToTalkButton({ onTranscript, disabled = false, className }: PushToTalkButtonProps) {
  const canListen = useCompanionStore((s) => s.voice.canListen);
  const phase = useCompanionStore((s) => s.voicePhase);

  if (!canListen) {
    return null;
  }

  const release = async () => {
    const text = await endPushToTalk();
    if (text) {
      onTranscript(text);
    }
  };

  const recording = phase === "recording";
  const transcribing = phase === "transcribing";

  return (
    <button
      type="button"
      className={[styles.button, className ?? ""].filter(Boolean).join(" ")}
      data-testid="push-to-talk"
      data-phase={phase}
      aria-label="Hold to talk"
      aria-pressed={recording}
      title="Hold to talk"
      disabled={disabled || transcribing}
      onPointerDown={(event) => {
        if (event.button !== 0) {
          return;
        }
        event.currentTarget.setPointerCapture(event.pointerId);
        beginPushToTalk();
      }}
      onPointerUp={() => void release()}
      onPointerCancel={() => void release()}
      onKeyDown={(event) => {
        if (isHoldKey(event)) {
          event.preventDefault();
          if (!event.repeat) {
            beginPushToTalk();
          }
        }
      }}
      onKeyUp={(event) => {
        if (isHoldKey(event)) {
          event.preventDefault();
          void release();
        }
      }}
      onBlur={() => void release()}
      onContextMenu={(event) => event.preventDefault()}
    >
      {transcribing ? <Loader2 size={15} className="animate-spin" aria-hidden="true" /> : <Mic size={15} aria-hidden="true" />}
    </button>
  );
}
