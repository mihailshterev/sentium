import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router";
import { SearchX } from "lucide-react";
import { useRole } from "@/shared/auth/hooks/useRole";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { Page } from "@/shared/ui/page";
import { SkeletonText } from "@/shared/ui/skeleton";
import { SCOPES, SETTINGS_SECTIONS } from "../catalog/settings-catalog";
import { matchesSearch } from "../catalog/settings-rules";
import { SettingsSaveBar, type SaveOutcome } from "../components/settings-save-bar";
import { SettingsSection } from "../components/settings-section";
import { SettingsSidebar, type SidebarGroup } from "../components/settings-sidebar";
import { useActiveSection } from "../hooks/useActiveSection";
import { useSettingsDraft } from "../hooks/useSettingsDraft";
import { useSaveSettingsGroups, useSettingsGroups } from "../hooks/useSettingsGroups";
import type { SettingsKey, SettingValue } from "../types/settings";
import styles from "../settings.module.css";

const SAVED_MESSAGE_MS = 4000;
const QUERY_PARAM = "q";
const FILTER_PARAM = "filter";
const MODIFIED_FILTER = "modified";

const PAGE_TITLE = "Settings";
const PAGE_SUBTITLE = "Your preferences and household-wide configuration";

const prefersReducedMotion = () => window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ?? false;

export function SettingsView() {
  const { isSovereign } = useRole();
  const { section: sectionParam } = useParams();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const query = searchParams.get(QUERY_PARAM) ?? "";
  const modifiedOnly = searchParams.get(FILTER_PARAM) === MODIFIED_FILTER;

  const sections = useMemo(
    () => SETTINGS_SECTIONS.filter((s) => isSovereign || s.scope === "personal"),
    [isSovereign],
  );
  const definitions = useMemo(() => sections.flatMap((s) => s.settings), [sections]);
  const keys = useMemo(() => sections.map((s) => s.group), [sections]);

  const { envelopes, errors: loadErrors, isLoading } = useSettingsGroups(keys);
  const draft = useSettingsDraft(definitions, envelopes);
  const save = useSaveSettingsGroups();
  const [outcome, setOutcome] = useState<SaveOutcome>(null);
  const [saveErrors, setSaveErrors] = useState<Partial<Record<SettingsKey, Error>>>({});

  const isFiltering = query.trim() !== "" || modifiedOnly;
  const filtered = sections.map((section) => ({
    section,
    visible: section.settings.filter(
      (d) =>
        matchesSearch(d, section, query) &&
        (!modifiedOnly || draft.isDirty(d.id) || draft.valueOf(d.id) !== d.defaultValue),
    ),
  }));
  const shown = filtered.filter((f) => f.visible.length > 0);

  // Sections only mount once loaded, so observe nothing until then.
  const [activeId, setActiveId] = useActiveSection(isLoading ? [] : shown.map((f) => f.section.id));

  const hasUnsaved = draft.dirtyCount > 0;
  useEffect(() => {
    if (!hasUnsaved) {
      return;
    }
    const warn = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [hasUnsaved]);

  useEffect(() => {
    if (outcome?.kind !== "saved") {
      return;
    }
    const timeout = setTimeout(() => setOutcome(null), SAVED_MESSAGE_MS);
    return () => clearTimeout(timeout);
  }, [outcome]);

  const deepLinked = useRef(false);
  useEffect(() => {
    if (isLoading || deepLinked.current || !sectionParam) {
      return;
    }
    deepLinked.current = true;
    document.getElementById(sectionParam)?.scrollIntoView({ block: "start" });
    setActiveId(sectionParam);
  }, [isLoading, sectionParam, setActiveId]);

  if (isLoading) {
    return (
      <Page title={PAGE_TITLE} subtitle={PAGE_SUBTITLE}>
        <Card padding="lg" aria-busy="true">
          <p className={styles.loadingText}>Loading settings…</p>
          <SkeletonText lines={4} />
        </Card>
      </Page>
    );
  }

  const updateParam = (name: string, value: string | null) =>
    setSearchParams(
      (params) => {
        if (value) {
          params.set(name, value);
        } else {
          params.delete(name);
        }
        return params;
      },
      { replace: true },
    );

  const selectSection = (id: string) => {
    document
      .getElementById(id)
      ?.scrollIntoView({ behavior: prefersReducedMotion() ? "auto" : "smooth", block: "start" });
    setActiveId(id);
    navigate({ pathname: `/settings/${id}`, search: searchParams.toString() }, { replace: true });
  };

  const changeSetting = (id: string, value: SettingValue) => {
    draft.setValue(id, value);
    setOutcome(null);
  };

  const saveChanges = async () => {
    const results = await save.mutateAsync(draft.patches());
    const failed = results.filter((r) => r.error);
    draft.clearGroups(results.filter((r) => !r.error).map((r) => r.key));
    setSaveErrors(Object.fromEntries(failed.map((r) => [r.key, r.error])));
    setOutcome(
      failed.length > 0
        ? { kind: "failed", sections: failed.map((r) => sections.find((s) => s.group === r.key)?.title ?? r.key) }
        : { kind: "saved" },
    );
  };

  const discardChanges = () => {
    draft.discard();
    setSaveErrors({});
    setOutcome(null);
  };

  const sidebarGroups: SidebarGroup[] = SCOPES.map((scope) => ({
    scope: scope.id,
    title: scope.title,
    description: scope.description,
    sections: filtered
      .filter((f) => f.section.scope === scope.id)
      .map((f) => ({
        section: f.section,
        visibleCount: f.visible.length,
        hasUnsaved: f.section.settings.some((d) => draft.isDirty(d.id)),
      })),
  })).filter((g) => g.sections.length > 0);

  const householdZone = envelopes.calendar?.value.timeZone;
  const inheritZoneLabel =
    typeof householdZone === "string" ? `Household default (${householdZone})` : "Household default";

  return (
    <Page title={PAGE_TITLE} subtitle={PAGE_SUBTITLE}>
      <div className={styles.layout}>
        <SettingsSidebar
          groups={sidebarGroups}
          activeId={activeId}
          query={query}
          modifiedOnly={modifiedOnly}
          isFiltering={isFiltering}
          onQueryChange={(value) => updateParam(QUERY_PARAM, value || null)}
          onModifiedOnlyChange={(value) => updateParam(FILTER_PARAM, value ? MODIFIED_FILTER : null)}
          onSelect={selectSection}
        />

        <div className={styles.content}>
          {shown.length === 0 ? (
            <EmptyState
              icon={<SearchX />}
              title="No settings match"
              hint={modifiedOnly ? "Nothing differs from its default with this search." : "Try another word."}
              action={
                <Button
                  size="sm"
                  onClick={() =>
                    setSearchParams(
                      (params) => {
                        params.delete(QUERY_PARAM);
                        params.delete(FILTER_PARAM);
                        return params;
                      },
                      { replace: true },
                    )
                  }
                >
                  Clear filters
                </Button>
              }
            />
          ) : (
            SCOPES.map((scope) => {
              const scoped = shown.filter((f) => f.section.scope === scope.id);
              if (scoped.length === 0) {
                return null;
              }
              return (
                <div key={scope.id} className={styles.scope}>
                  <p className={styles.scopeLabel}>
                    {scope.title.toLowerCase()} <span>· {scope.description.toLowerCase()}</span>
                  </p>
                  {scoped.map(({ section, visible }) => (
                    <SettingsSection
                      key={section.id}
                      section={section}
                      visible={visible}
                      envelope={envelopes[section.group]}
                      loadError={loadErrors[section.group]}
                      saveError={saveErrors[section.group]}
                      inheritZoneLabel={inheritZoneLabel}
                      valueOf={draft.valueOf}
                      isDirty={draft.isDirty}
                      errors={draft.errors}
                      onChange={changeSetting}
                    />
                  ))}
                </div>
              );
            })
          )}

          <SettingsSaveBar
            dirtyCount={draft.dirtyCount}
            invalidCount={Object.keys(draft.errors).length}
            isSaving={save.isPending}
            outcome={outcome}
            onSave={() => void saveChanges()}
            onDiscard={discardChanges}
          />
        </div>
      </div>
    </Page>
  );
}
