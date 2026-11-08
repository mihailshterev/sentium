export const describeTools = (tools: string[] | null | undefined) =>
  tools == null
    ? "all built-in tools"
    : tools.length === 0
      ? "no tools"
      : `${tools.length} tool${tools.length === 1 ? "" : "s"}`;
