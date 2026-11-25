export const conversationKeys = {
  all: ["conversations"] as const,
  list: (search: string, archived: boolean) => [...conversationKeys.all, "list", search, archived] as const,
};
