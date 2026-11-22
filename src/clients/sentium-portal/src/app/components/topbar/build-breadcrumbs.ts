import { findNavGroup, findNavLink } from "../navbar/nav-config";

export interface Crumb {
  label: string;
  /** Absent for the current page and for groups, which have no page of their own. */
  to?: string;
}

/** Labels for pages below a sidebar link; the page heading carries the specific name. */
const DETAIL_LABELS: [RegExp, string][] = [
  [/^\/workflows\/new$/, "New workflow"],
  [/^\/workflows\/[^/]+$/, "Workflow"],
  [/^\/assistant\/[^/]+$/, "Conversation"],
  [/^\/orchestration\/runs\/[^/]+$/, "Run"],
  [/^\/workspaces\/[^/]+$/, "Workspace"],
  [/^\/inventory\/assets\/[^/]+$/, "Asset"],
  [/^\/evaluations\/suites\/[^/]+$/, "Eval suite"],
  [/^\/evaluations\/benchmarks\/[^/]+$/, "Benchmark"],
  [/^\/evaluations\/[^/]+$/, "Evaluation"],
  [/^\/sandbox\/[^/]+$/, "Run"],
];

const SLUG = /^[a-z]+(-[a-z]+)*$/;

function detailLabel(pathname: string): string {
  const known = DETAIL_LABELS.find(([pattern]) => pattern.test(pathname));
  if (known) {
    return known[1];
  }

  // Readable slugs such as `/settings/appearance` name themselves; ids do not.
  const last = pathname.split("/").filter(Boolean).at(-1) ?? "";
  return SLUG.test(last) ? last.charAt(0).toUpperCase() + last.slice(1).replaceAll("-", " ") : "Details";
}

export function buildBreadcrumbs(pathname: string): Crumb[] {
  const path = pathname.length > 1 ? pathname.replace(/\/+$/, "") : pathname;
  if (path === "/") {
    return [{ label: "Home" }];
  }

  const crumbs: Crumb[] = [{ label: "Home", to: "/" }];
  const group = findNavGroup(path);
  if (group) {
    crumbs.push({ label: group.label });
  }

  const link = findNavLink(path);
  if (!link) {
    return crumbs;
  }

  const isDetail = path !== link.to;
  crumbs.push({ label: link.label, to: isDetail ? link.to : undefined });
  if (isDetail) {
    crumbs.push({ label: detailLabel(path) });
  }
  return crumbs;
}
