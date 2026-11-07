import { useEffect, useState } from "react";
import styles from "../inventory.module.css";
import { SearchInput, Select, Switch } from "@/shared/ui/form-controls";
import type { AssetFilters, AssetSort, TagCount } from "../types/inventory";
import { TagInput } from "./tag-input";

const SEARCH_DEBOUNCE_MS = 300;

interface AssetFilterBarProps {
  filters: AssetFilters;
  onChange: (changes: Partial<AssetFilters>) => void;
  categories: string[];
  tags: TagCount[];
}

export function AssetFilterBar({ filters, onChange, categories, tags }: AssetFilterBarProps) {
  const [query, setQuery] = useState(filters.q ?? "");
  const [syncedQuery, setSyncedQuery] = useState(filters.q ?? "");

  if ((filters.q ?? "") !== syncedQuery) {
    setSyncedQuery(filters.q ?? "");
    setQuery(filters.q ?? "");
  }

  useEffect(() => {
    if ((filters.q ?? "") === query) return;
    const timer = setTimeout(() => onChange({ q: query }), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [query, filters.q, onChange]);

  const shared = filters.agentAccessible == null ? "all" : filters.agentAccessible ? "shared" : "private";

  return (
    <div className={styles.filterBar} role="search">
      <SearchInput
        aria-label="Search assets"
        placeholder="Search name, brand, model, serial, tag or room…"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        wrapperClassName={styles.filterSearch}
      />
      <Select
        aria-label="Category"
        value={filters.category ?? ""}
        onChange={(e) => onChange({ category: e.target.value || null })}
        controlSize="sm"
      >
        <option value="">All categories</option>
        {categories.map((c) => (
          <option key={c} value={c}>
            {c}
          </option>
        ))}
      </Select>
      <Select
        aria-label="Shared with assistants"
        value={shared}
        onChange={(e) => onChange({ agentAccessible: e.target.value === "all" ? null : e.target.value === "shared" })}
        controlSize="sm"
      >
        <option value="all">Shared & private</option>
        <option value="shared">Shared with assistants</option>
        <option value="private">Private only</option>
      </Select>
      <Select
        aria-label="Sort"
        value={filters.sort ?? "Name"}
        onChange={(e) => onChange({ sort: e.target.value as AssetSort })}
        controlSize="sm"
      >
        <option value="Name">Name</option>
        <option value="Updated">Recently updated</option>
        <option value="Created">Recently added</option>
      </Select>
      <div className={styles.filterTags}>
        <TagInput
          aria-label="Filter by tags"
          value={filters.tags ?? []}
          onChange={(next) => onChange({ tags: next })}
          suggestions={tags.map((t) => t.name)}
          placeholder="Filter by tag…"
        />
      </div>
      {filters.locationId && (
        <label className={styles.filterToggle}>
          <Switch
            checked={filters.includeSubLocations !== false}
            onChange={(e) => onChange({ includeSubLocations: e.target.checked })}
          />
          Include sub-locations
        </label>
      )}
    </div>
  );
}
