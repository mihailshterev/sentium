import { ArrowUpRight } from "lucide-react";
import { useProfile } from "@/features/profile/hooks/useProfile";
import styles from "../assistant.module.css";

interface WelcomeScreenProps {
  suggestions: string[];
  onSelectSuggestion: (text: string) => void;
}

function greetingFor(hour: number): string {
  if (hour >= 5 && hour < 12) {
    return "Good morning";
  }
  if (hour >= 12 && hour < 18) {
    return "Good afternoon";
  }
  return "Good evening";
}

export function WelcomeScreen({ suggestions, onSelectSuggestion }: WelcomeScreenProps) {
  const { profile } = useProfile();
  const name = profile?.firstName ? `, ${profile.firstName}` : "";

  return (
    <div className={styles.welcome}>
      <img src="/sentium-mark.png" alt="" className={styles.welcomeMark} width={44} height={44} />
      <h1 className={styles.welcomeTitle}>
        {greetingFor(new Date().getHours())}
        {name}
      </h1>
      <h2 className={styles.welcomeSubtitle}>How can I help today?</h2>
      <p className={styles.welcomeMeta}>Ask a question, attach workspace context, or start from one of these.</p>
      <div className={styles.suggestions}>
        {suggestions.map((s) => (
          <button key={s} type="button" className={styles.suggestionChip} onClick={() => onSelectSuggestion(s)}>
            <span>{s}</span>
            <ArrowUpRight size={14} aria-hidden="true" />
          </button>
        ))}
      </div>
    </div>
  );
}
