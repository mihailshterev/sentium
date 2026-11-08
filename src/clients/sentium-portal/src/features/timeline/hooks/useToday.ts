import { useEffect, useState } from "react";

export function useToday(): Date {
  const [today, setToday] = useState(() => new Date());

  useEffect(() => {
    const timer = setInterval(() => {
      const now = new Date();
      setToday((previous) => (previous.toDateString() === now.toDateString() ? previous : now));
    }, 60_000);
    return () => clearInterval(timer);
  }, []);

  return today;
}
