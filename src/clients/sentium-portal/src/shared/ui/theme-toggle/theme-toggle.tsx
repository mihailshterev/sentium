import { Moon, Sun } from "lucide-react";
import { resolveTheme, useThemeStore } from "@/shared/ui/state/theme-store";
import { buttonClass } from "@/shared/ui/button";

interface ThemeToggleProps {
  className?: string;
}

/** Icon button switching between dark and light; shows the theme it will switch to. */
export function ThemeToggle({ className }: ThemeToggleProps) {
  const preference = useThemeStore((s) => s.preference);
  const toggle = useThemeStore((s) => s.toggle);

  const isDark = resolveTheme(preference) === "dark";
  const label = isDark ? "Switch to light theme" : "Switch to dark theme";

  return (
    <button
      type="button"
      className={buttonClass({ variant: "ghost", size: "sm", iconOnly: true, className })}
      onClick={toggle}
      aria-label={label}
    >
      {isDark ? <Sun size={16} /> : <Moon size={16} />}
    </button>
  );
}
