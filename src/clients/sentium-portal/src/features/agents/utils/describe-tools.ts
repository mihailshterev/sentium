export const describeTools = (tools: string[] | null | undefined) =>
  tools == null
    ? "all tools"
    : tools.length === 0
      ? "no tools"
      : `${tools.length} tool${tools.length === 1 ? "" : "s"}`;
