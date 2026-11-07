import { useMemo, useRef, useState } from "react";
import { ChevronRight, Home, MapPin, Pencil, Plus, Trash2 } from "lucide-react";
import styles from "../inventory.module.css";
import type { InventoryLocation, LocationTreeNode } from "../types/inventory";
import { ancestorsOf, buildTree } from "../utils/location-tree";

const ALL = "__all__";

interface LocationTreeProps {
  locations: InventoryLocation[];
  selectedId: string | null;
  onSelect: (id: string | null) => void;
  onAddChild: (parent: InventoryLocation) => void;
  onEdit: (location: InventoryLocation) => void;
  onDelete: (location: InventoryLocation) => void;
}

interface VisibleRow {
  node: LocationTreeNode;
  level: number;
  parentId: string | null;
}

export function LocationTree({ locations, selectedId, onSelect, onAddChild, onEdit, onDelete }: LocationTreeProps) {
  const roots = useMemo(() => buildTree(locations), [locations]);
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());
  const [focusedId, setFocusedId] = useState<string>(selectedId ?? ALL);
  const itemRefs = useRef(new Map<string, HTMLLIElement>());

  const forcedOpen = useMemo(
    () => new Set(selectedId ? ancestorsOf(locations, selectedId) : []),
    [locations, selectedId],
  );
  const isExpanded = (id: string) => forcedOpen.has(id) || !collapsed.has(id);

  const visible = useMemo(() => {
    const rows: VisibleRow[] = [];
    const walk = (nodes: LocationTreeNode[], level: number, parentId: string | null) => {
      for (const node of nodes) {
        rows.push({ node, level, parentId });
        if (node.children.length > 0 && (forcedOpen.has(node.id) || !collapsed.has(node.id))) {
          walk(node.children, level + 1, node.id);
        }
      }
    };
    walk(roots, 2, null);
    return rows;
  }, [roots, collapsed, forcedOpen]);

  const order = [ALL, ...visible.map((r) => r.node.id)];

  const focus = (id: string) => {
    setFocusedId(id);
    itemRefs.current.get(id)?.focus();
  };

  const toggle = (id: string, open?: boolean) =>
    setCollapsed((prev) => {
      const next = new Set(prev);
      const shouldOpen = open ?? next.has(id);
      if (shouldOpen) next.delete(id);
      else next.add(id);
      return next;
    });

  const onKeyDown = (event: React.KeyboardEvent<HTMLUListElement>) => {
    const index = order.indexOf(focusedId);
    const row = visible.find((r) => r.node.id === focusedId);

    switch (event.key) {
      case "ArrowDown":
        focus(order[Math.min(index + 1, order.length - 1)]);
        break;
      case "ArrowUp":
        focus(order[Math.max(index - 1, 0)]);
        break;
      case "Home":
        focus(order[0]);
        break;
      case "End":
        focus(order[order.length - 1]);
        break;
      case "ArrowRight":
        if (row && row.node.children.length > 0) {
          if (isExpanded(row.node.id)) focus(row.node.children[0].id);
          else toggle(row.node.id, true);
        }
        break;
      case "ArrowLeft":
        if (row) {
          if (row.node.children.length > 0 && isExpanded(row.node.id) && !forcedOpen.has(row.node.id)) {
            toggle(row.node.id, false);
          } else {
            focus(row.parentId ?? ALL);
          }
        }
        break;
      case "Enter":
      case " ":
        onSelect(focusedId === ALL ? null : focusedId);
        break;
      default:
        return;
    }
    event.preventDefault();
  };

  const totalAssets = roots.reduce((sum, r) => sum + r.subtreeAssetCount, 0);

  return (
    <ul role="tree" aria-label="Locations" className={styles.tree} onKeyDown={onKeyDown}>
      <li
        ref={(el) => {
          if (el) itemRefs.current.set(ALL, el);
        }}
        role="treeitem"
        aria-level={1}
        aria-selected={selectedId === null}
        tabIndex={focusedId === ALL ? 0 : -1}
        className={styles.treeItem}
        data-active={selectedId === null || undefined}
        onClick={() => {
          setFocusedId(ALL);
          onSelect(null);
        }}
      >
        <span className={styles.treeRow} style={{ "--tree-level": 0 } as React.CSSProperties}>
          <span className={styles.treeToggleSpacer} aria-hidden="true" />
          <Home size={15} aria-hidden="true" className={styles.treeIcon} />
          <span className={styles.treeLabel}>All locations</span>
          <span className={styles.treeCount}>{totalAssets}</span>
        </span>
      </li>

      {visible.map(({ node, level }) => {
        const hasChildren = node.children.length > 0;
        const expanded = hasChildren ? isExpanded(node.id) : undefined;
        const selected = node.id === selectedId;

        return (
          <li
            key={node.id}
            ref={(el) => {
              if (el) itemRefs.current.set(node.id, el);
              else itemRefs.current.delete(node.id);
            }}
            role="treeitem"
            aria-level={level}
            aria-expanded={expanded}
            aria-selected={selected}
            aria-label={node.name}
            tabIndex={focusedId === node.id ? 0 : -1}
            className={styles.treeItem}
            data-active={selected || undefined}
            data-testid={`location-${node.name}`}
            onClick={(e) => {
              e.stopPropagation();
              setFocusedId(node.id);
              onSelect(node.id);
            }}
          >
            <span className={styles.treeRow} style={{ "--tree-level": level - 1 } as React.CSSProperties}>
              {hasChildren ? (
                <button
                  type="button"
                  tabIndex={-1}
                  className={styles.treeToggle}
                  data-open={expanded || undefined}
                  aria-label={expanded ? `Collapse ${node.name}` : `Expand ${node.name}`}
                  onClick={(e) => {
                    e.stopPropagation();
                    toggle(node.id);
                  }}
                >
                  <ChevronRight size={14} />
                </button>
              ) : (
                <span className={styles.treeToggleSpacer} aria-hidden="true" />
              )}
              <MapPin size={14} aria-hidden="true" className={styles.treeIcon} />
              <span className={styles.treeLabel} title={node.path}>
                {node.name}
              </span>
              <span className={styles.treeCount} title={`${node.subtreeAssetCount} item(s) here and below`}>
                {node.subtreeAssetCount}
              </span>
              <span className={styles.treeActions}>
                <button
                  type="button"
                  tabIndex={-1}
                  className={styles.treeAction}
                  aria-label={`Add a location inside ${node.name}`}
                  title="Add sub-location"
                  onClick={(e) => {
                    e.stopPropagation();
                    onAddChild(node);
                  }}
                >
                  <Plus size={13} />
                </button>
                <button
                  type="button"
                  tabIndex={-1}
                  className={styles.treeAction}
                  aria-label={`Edit ${node.name}`}
                  title="Edit"
                  onClick={(e) => {
                    e.stopPropagation();
                    onEdit(node);
                  }}
                >
                  <Pencil size={13} />
                </button>
                <button
                  type="button"
                  tabIndex={-1}
                  className={styles.treeAction}
                  data-danger
                  data-testid={`location-delete-${node.name}`}
                  aria-label={`Delete ${node.name}`}
                  title="Delete"
                  onClick={(e) => {
                    e.stopPropagation();
                    onDelete(node);
                  }}
                >
                  <Trash2 size={13} />
                </button>
              </span>
            </span>
          </li>
        );
      })}
    </ul>
  );
}
