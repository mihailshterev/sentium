import { useState } from "react";
import { Sparkles, Lightbulb, Compass, Zap, Telescope, FlaskConical, Rocket } from "lucide-react";
import useProfile from "../../../hooks/useProfile";
import styles from "../assistant.module.scss";

interface WelcomeScreenProps {
  suggestions: string[];
  onSelectSuggestion: (text: string) => void;
}

type Greeting = {
  icon: React.ReactNode;
  title: (name: string) => string;
  subtitle: string;
  meta: string;
};

const GREETINGS_BY_PERIOD: Record<"morning" | "afternoon" | "evening" | "night", Greeting[]> = {
  morning: [
    {
      icon: <Rocket size={38} />,
      title: (n) => `Morning${n} - Let's Launch Something Big`,
      subtitle: "The day is blank. What are we building?",
      meta: "Fresh mind, fresh ideas. Throw your first thought at me.",
    },
    {
      icon: <Lightbulb size={38} />,
      title: (n) => `Rise and Ideate${n}`,
      subtitle: "Your best ideas happen before lunch.",
      meta: "Give me a spark - I'll fan it into something remarkable.",
    },
    {
      icon: <Compass size={38} />,
      title: (n) => `Good Morning${n} - Which Direction Today?`,
      subtitle: "Every great journey starts with a single question.",
      meta: "Pick a direction, any direction - we'll navigate from there.",
    },
  ],
  afternoon: [
    {
      icon: <Zap size={38} />,
      title: (n) => `Afternoon Surge${n}`,
      subtitle: "Momentum is everything - let's keep it going.",
      meta: "Tell me what's on your mind and we'll turn it into something.",
    },
    {
      icon: <FlaskConical size={38} />,
      title: (n) => `Ready to Experiment${n}?`,
      subtitle: "Afternoons are perfect for trying something unexpected.",
      meta: "Bring me a problem, a hunch, or a half-baked idea.",
    },
    {
      icon: <Sparkles size={38} />,
      title: (n) => `Let's Create Something${n}`,
      subtitle: "What Shall We Explore Together?",
      meta: "Throw me a wild idea, a half-formed thought, or a big question - let's brainstorm.",
    },
  ],
  evening: [
    {
      icon: <Telescope size={38} />,
      title: (n) => `Evening Exploration${n}`,
      subtitle: "Some of the best ideas come after hours.",
      meta: "No constraints, no rush - just you, me, and a good idea.",
    },
    {
      icon: <Sparkles size={38} />,
      title: (n) => `The Evening is Ours${n}`,
      subtitle: "Creativity loves the quiet hours.",
      meta: "Bring me something to chew on - big, weird, or half-finished.",
    },
    {
      icon: <Lightbulb size={38} />,
      title: (n) => `Good Evening${n} - Still Thinking?`,
      subtitle: "The best breakthroughs don't wait for morning.",
      meta: "Share what's been rattling around in your head - let's figure it out.",
    },
  ],
  night: [
    {
      icon: <Telescope size={38} />,
      title: (n) => `Burning the Midnight Oil${n}?`,
      subtitle: "Night owls have the best ideas.",
      meta: "Whatever's keeping you up - let's dig into it together.",
    },
    {
      icon: <Zap size={38} />,
      title: (n) => `Late Night${n} - Let's Make It Count`,
      subtitle: "The world is quiet. Your thoughts are loud.",
      meta: "Tell me what you're working on and let's make progress.",
    },
    {
      icon: <FlaskConical size={38} />,
      title: (n) => `Still Here${n}? Good.`,
      subtitle: "The best experiments happen when nobody's watching.",
      meta: "Go on - hit me with the idea you've been sitting on all day.",
    },
  ],
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

const WelcomeScreen = ({ suggestions, onSelectSuggestion }: WelcomeScreenProps) => {
  const { profile } = useProfile();
  const firstName = profile?.firstName ? `, ${profile.firstName}` : "";

  const [greeting] = useState(() => {
    const pool = GREETINGS_BY_PERIOD[getPeriod()];
    return pool[Math.floor(Math.random() * pool.length)];
  });

  return (
    <div className={styles.welcomeScreen}>
      <div className={styles.welcomeIconWrap}>{greeting.icon}</div>
      <h1 className={styles.welcomeTitle}>{greeting.title(firstName)}</h1>
      <h2 className={styles.welcomeSubtitle}>{greeting.subtitle}</h2>
      <p className={styles.welcomeMeta}>{greeting.meta}</p>
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
