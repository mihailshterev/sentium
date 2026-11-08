import { useCallback, useEffect, useState } from "react";
import { STATUS_MESSAGES } from "../status-messages";

export function useMessageListUi(isTyping: boolean) {
  const [expandedThoughts, setExpandedThoughts] = useState<Set<string>>(new Set());
  const [copiedMessageId, setCopiedMessageId] = useState<string | null>(null);
  const [statusIndex, setStatusIndex] = useState(0);
  const [statusVisible, setStatusVisible] = useState(true);

  const toggleThought = useCallback(
    (id: string) =>
      setExpandedThoughts((prev) => {
        const next = new Set(prev);
        if (next.has(id)) {
          next.delete(id);
        } else {
          next.add(id);
        }
        return next;
      }),
    [],
  );

  const copyMessage = useCallback((content: string, id: string) => {
    navigator.clipboard.writeText(content).then(() => {
      setCopiedMessageId(id);
      setTimeout(() => setCopiedMessageId(null), 2000);
    });
  }, []);

  const resetStatus = useCallback(() => {
    setStatusIndex(0);
    setStatusVisible(true);
  }, []);

  useEffect(() => {
    if (!isTyping) {
      return;
    }
    let cancelled = false;
    const interval = setInterval(() => {
      setStatusVisible(false);
      setTimeout(() => {
        if (!cancelled) {
          setStatusIndex((prev) => (prev + 1) % STATUS_MESSAGES.length);
          setStatusVisible(true);
        }
      }, 300);
    }, 2500);
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [isTyping]);

  return { expandedThoughts, toggleThought, copiedMessageId, copyMessage, statusIndex, statusVisible, resetStatus };
}
