export const AUTH_STATUS = {
  IDLE: "idle",
  CHECKING: "checking",
  AUTHENTICATED: "authenticated",
  UNAUTHENTICATED: "unauthenticated",
} as const;

/**
 * No model chosen yet: the assistant switches to the server's default model once the model list loads,
 * and the server applies its default model to an empty model id in the meantime.
 */
export const DEFAULT_ASSISTANT_MODEL = "";

export type SuggestionKind = "write" | "code" | "summarize" | "plan" | "ideate" | "learn" | "analyze";

export interface Suggestion {
  text: string;
  kind: SuggestionKind;
}

export const SUGGESTIONS_POOL: Suggestion[] = [
  { text: "Summarize this long document", kind: "summarize" },
  { text: "Write a professional email", kind: "write" },
  { text: "Help me debug some code", kind: "code" },
  { text: "Plan a weekly workout routine", kind: "plan" },
  { text: "Brainstorm 5 ideas for a blog", kind: "ideate" },
  { text: "Write a short sci-fi story", kind: "write" },
  { text: "Suggest a naming scheme for a project", kind: "ideate" },
  { text: "Help me write a birthday poem", kind: "write" },
  { text: "Explain quantum physics like I'm five", kind: "learn" },
  { text: "What are some healthy dinner ideas?", kind: "ideate" },
  { text: "Teach me a basic Spanish phrase", kind: "learn" },
  { text: "Give me a fun fact about space", kind: "learn" },
  { text: "Compare these two software options", kind: "analyze" },
  { text: "Analyze these trend patterns", kind: "analyze" },
  { text: "Evaluate the risks of this plan", kind: "analyze" },
  { text: "Summarize recent tech news", kind: "summarize" },
];
