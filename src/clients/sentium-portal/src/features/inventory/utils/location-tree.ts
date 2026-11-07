import type { InventoryLocation, LocationTreeNode } from "../types/inventory";

const byName = (a: { name: string }, b: { name: string }) =>
  a.name.localeCompare(b.name, undefined, { sensitivity: "base" });

export function buildTree(locations: InventoryLocation[]): LocationTreeNode[] {
  const nodes = new Map<string, LocationTreeNode>();
  for (const location of locations) nodes.set(location.id, { ...location, children: [] });

  const roots: LocationTreeNode[] = [];
  for (const node of nodes.values()) {
    const parent = node.parentLocationId ? nodes.get(node.parentLocationId) : undefined;
    if (parent && parent.id !== node.id) parent.children.push(node);
    else roots.push(node);
  }

  const sort = (list: LocationTreeNode[], seen = new Set<string>()) => {
    list.sort(byName);
    for (const node of list) {
      if (seen.has(node.id)) continue;
      seen.add(node.id);
      sort(node.children, seen);
    }
  };
  sort(roots);
  return roots;
}

export function descendantsOf(locations: InventoryLocation[], id: string): Set<string> {
  const childrenOf = new Map<string, string[]>();
  for (const l of locations) {
    if (!l.parentLocationId) continue;
    const list = childrenOf.get(l.parentLocationId) ?? [];
    list.push(l.id);
    childrenOf.set(l.parentLocationId, list);
  }

  const result = new Set<string>();
  const stack = [id];
  while (stack.length > 0) {
    const current = stack.pop()!;
    if (result.has(current)) continue;
    result.add(current);
    stack.push(...(childrenOf.get(current) ?? []));
  }
  return result;
}

export function ancestorsOf(locations: InventoryLocation[], id: string): string[] {
  const byId = new Map(locations.map((l) => [l.id, l]));
  const result: string[] = [];
  let current = byId.get(id)?.parentLocationId ?? null;
  while (current && !result.includes(current)) {
    result.push(current);
    current = byId.get(current)?.parentLocationId ?? null;
  }
  return result;
}

export interface LocationOption {
  id: string;
  label: string;
  depth: number;
  path: string;
}

export function flattenForSelect(locations: InventoryLocation[], exclude: Set<string> = new Set()): LocationOption[] {
  const options: LocationOption[] = [];
  const visit = (nodes: LocationTreeNode[], depth: number) => {
    for (const node of nodes) {
      if (exclude.has(node.id)) continue;
      options.push({ id: node.id, label: `${"  ".repeat(depth)}${node.name}`, depth, path: node.path });
      visit(node.children, depth + 1);
    }
  };
  visit(buildTree(locations), 0);
  return options;
}
