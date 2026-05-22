import { Shield } from "lucide-react";
import styles from "../assistant.module.scss";

interface WelcomeScreenProps {
  suggestions: string[];
  onSelectSuggestion: (text: string) => void;
}

const WelcomeScreen = ({ suggestions, onSelectSuggestion }: WelcomeScreenProps) => {
  return (
    <div className={styles.welcomeScreen}>
      <div className={styles.welcomeIconWrap}>
        <Shield size={38} />
      </div>
      <h1 className={styles.welcomeTitle}>Good to See You!</h1>
      <h2 className={styles.welcomeSubtitle}>How Can I Assist You Today?</h2>
      <p className={styles.welcomeMeta}>I'm available 24/7 — ask me anything.</p>
      <div className={styles.suggestionRow}>
        {suggestions.map((s) => (
          <button key={s} className={styles.suggestionChip} onClick={() => onSelectSuggestion(s)}>
            {s}
          </button>
        ))}
      </div>
    </div>
  );
};

export default WelcomeScreen;
