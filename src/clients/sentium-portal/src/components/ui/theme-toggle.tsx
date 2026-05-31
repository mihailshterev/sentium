import { Moon, Sun } from "lucide-react";
import { resolveTheme, useThemeStore } from "../../stores/theme-store";
import styles from "./theme-toggle.module.scss";

interface ThemeToggleProps {
  className?: string;
}

const ThemeToggle = ({ className }: ThemeToggleProps) => {
  const preference = useThemeStore((s) => s.preference);
  const toggle = useThemeStore((s) => s.toggle);

  const resolved = resolveTheme(preference);
  const isDark = resolved === "dark";

  return (
    <button
      type="button"
      className={`${styles.toggle} ${className ?? ""}`}
      onClick={toggle}
      title={isDark ? "Switch to light theme" : "Switch to dark theme"}
      aria-label={isDark ? "Switch to light theme" : "Switch to dark theme"}
    >
      <span className={styles.track}>
        <span className={styles.thumb} data-theme={resolved}>
          {isDark ? <Moon size={12} /> : <Sun size={12} />}
        </span>
      </span>
    </button>
  );
};

export default ThemeToggle;
