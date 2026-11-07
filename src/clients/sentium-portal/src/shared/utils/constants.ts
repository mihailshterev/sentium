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

export const SUGGESTIONS_POOL = [
  "Summarize this long document",
  "Write a professional email",
  "Help me debug some code",
  "Plan a weekly workout routine",
  "Brainstorm 5 ideas for a blog",
  "Write a short sci-fi story",
  "Suggest a naming scheme for a project",
  "Help me write a birthday poem",
  "Explain quantum physics like I'm five",
  "What are some healthy dinner ideas?",
  "Teach me a basic Spanish phrase",
  "Give me a fun fact about space",
  "Compare these two software options",
  "Analyze these trend patterns",
  "Evaluate the risks of this plan",
  "Summarize recent tech news",
];
