import { useState } from "react";
import {
  ArrowUpRight,
  CalendarCheck,
  ChartLine,
  CodeXml,
  GraduationCap,
  Lightbulb,
  PenLine,
  ScrollText,
  type LucideIcon,
} from "lucide-react";
import { useProfile } from "@/features/profile/hooks/useProfile";
import type { Suggestion, SuggestionKind } from "@/shared/utils/constants";
import styles from "../assistant.module.css";

interface WelcomeScreenProps {
  suggestions: Suggestion[];
  onSelectSuggestion: (text: string) => void;
}

const SUGGESTION_ICONS: Record<SuggestionKind, LucideIcon> = {
  write: PenLine,
  code: CodeXml,
  summarize: ScrollText,
  plan: CalendarCheck,
  ideate: Lightbulb,
  learn: GraduationCap,
  analyze: ChartLine,
};

const GREETING_BY_PERIOD: Record<"morning" | "afternoon" | "evening" | "night", string> = {
  morning: "Good morning",
  afternoon: "Good afternoon",
  evening: "Good evening",
  night: "Working late",
};

function getPeriod(): "morning" | "afternoon" | "evening" | "night" {
  const hour = new Date().getHours();
  if (hour >= 5 && hour < 12) {
    return "morning";
  }
  if (hour >= 12 && hour < 17) {
    return "afternoon";
  }
  if (hour >= 17 && hour < 21) {
    return "evening";
  }
  return "night";
}

export function WelcomeScreen({ suggestions, onSelectSuggestion }: WelcomeScreenProps) {
  const { profile } = useProfile();
  const firstName = profile?.firstName ? `, ${profile.firstName}` : "";

  const [greeting] = useState(() => GREETING_BY_PERIOD[getPeriod()]);

  return (
    <div className={styles.welcome}>
      <h1 className={styles.welcomeTitle}>
        {greeting}
        {firstName}
      </h1>
      <h2 className={styles.welcomeSubtitle}>How can I help?</h2>
      <div className={styles.suggestions}>
        {suggestions.map(({ text, kind }) => {
          const Icon = SUGGESTION_ICONS[kind];
          return (
            <button key={text} type="button" className={styles.suggestionChip} onClick={() => onSelectSuggestion(text)}>
              <span className={styles.suggestionIcon} aria-hidden="true">
                <Icon size={16} />
              </span>
              <span className={styles.suggestionBody}>
                <span className={styles.suggestionKind} aria-hidden="true">
                  {kind}
                </span>
                <span className={styles.suggestionText}>{text}</span>
              </span>
              <ArrowUpRight size={14} className={styles.suggestionArrow} aria-hidden="true" />
            </button>
          );
        })}
      </div>
    </div>
  );
}
