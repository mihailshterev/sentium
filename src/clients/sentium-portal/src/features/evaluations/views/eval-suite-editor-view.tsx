import { useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router";
import { ArrowLeft, Check, Columns3, FlaskConical, History, Play, Plus, Trash2 } from "lucide-react";
import styles from "../evaluations.module.css";
import { useToolCatalog } from "@/features/agents/hooks/useToolCatalog";
import type { ToolDescriptor } from "@/features/agents/types/agents";
import { ApiError } from "@/shared/api/client";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { Input, Select, Textarea } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { Page } from "@/shared/ui/page";
import { Skeleton } from "@/shared/ui/skeleton";
import { StatusMessage } from "@/shared/ui/status-message";
import { EvalCaseEditor } from "../components/eval-case-editor";
import { StartBenchmarkDialog } from "../components/start-benchmark-dialog";
import { StartEvaluationDialog } from "../components/start-evaluation-dialog";
import { useEvalSuite } from "../hooks/useEvalSuites";
import type { EvalSuite, EvalSuiteVersion } from "../types/eval-suites";
import { type CaseDraft, caseDraftErrors, caseLabel, emptyCaseDraft, toCaseDraft, toCaseInput } from "../utils/case-draft";

const NEW_SUITE = "new";

export function EvalSuiteEditorView() {
  const { suiteId } = useParams();
  const [searchParams] = useSearchParams();
  const isNew = suiteId === NEW_SUITE;
  const versionParam = searchParams.get("version");
  const version = versionParam ? Number(versionParam) : undefined;

  const { suite, isLoading, error, versions, createSuite, updateSuite, isSaving } = useEvalSuite(
    isNew ? undefined : suiteId,
    version,
  );
  const { tools } = useToolCatalog();
  // Lives here, not in the form: saving a new version remounts the form with the saved cases.
  const [notice, setNotice] = useState<string | null>(null);

  if (!isNew && isLoading) {
    return (
      <Page title="Eval Suite">
        <Skeleton height={240} />
      </Page>
    );
  }

  if (!isNew && (error || !suite)) {
    return (
      <Page title="Eval Suite">
        <EmptyState icon={<FlaskConical />} title="Eval Suite not found" hint="It may have been deleted." />
      </Page>
    );
  }

  return (
    <SuiteForm
      key={suite ? `${suite.id}-${suite.version}` : NEW_SUITE}
      suite={suite}
      versions={versions}
      tools={tools}
      isSaving={isSaving}
      notice={notice}
      onNotice={setNotice}
      onCreate={createSuite}
      onUpdate={updateSuite}
    />
  );
}

interface SuiteFormProps {
  suite: EvalSuite | undefined;
  versions: EvalSuiteVersion[];
  tools: ToolDescriptor[];
  isSaving: boolean;
  notice: string | null;
  onNotice: (notice: string | null) => void;
  onCreate: ReturnType<typeof useEvalSuite>["createSuite"];
  onUpdate: ReturnType<typeof useEvalSuite>["updateSuite"];
}

function SuiteForm({ suite, versions, tools, isSaving, notice, onNotice, onCreate, onUpdate }: SuiteFormProps) {
  const navigate = useNavigate();
  const [, setSearchParams] = useSearchParams();
  const readOnly = suite !== undefined && suite.version !== suite.currentVersion;

  const [name, setName] = useState(suite?.name ?? "");
  const [description, setDescription] = useState(suite?.description ?? "");
  const [cases, setCases] = useState<CaseDraft[]>(() => suite?.cases.map(toCaseDraft) ?? []);
  const [selectedKey, setSelectedKey] = useState<string | null>(cases[0]?.key ?? null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [evaluateOpen, setEvaluateOpen] = useState(false);
  const [benchmarkOpen, setBenchmarkOpen] = useState(false);

  const selected = cases.find((c) => c.key === selectedKey) ?? null;
  const errorsByKey = new Map(cases.map((c) => [c.key, caseDraftErrors(c)]));
  const allErrors = [
    ...(name.trim() ? [] : ["The suite needs a name."]),
    ...cases.flatMap((c, i) => (errorsByKey.get(c.key) ?? []).map((e) => `${caseLabel(c, i)}: ${e}`)),
  ];

  const addCase = () => {
    const draft = emptyCaseDraft();
    setCases((prev) => [...prev, draft]);
    setSelectedKey(draft.key);
  };

  const removeCase = (key: string) => {
    const remaining = cases.filter((c) => c.key !== key);
    setCases(remaining);
    setSelectedKey(remaining[0]?.key ?? null);
  };

  const showVersion = (value: string) => {
    const picked = Number(value);
    onNotice(null);
    setSearchParams(suite && picked !== suite.currentVersion ? { version: value } : {});
  };

  const save = async () => {
    setSaveError(null);
    onNotice(null);
    if (allErrors.length > 0) {
      setSaveError("Fix the problems listed below first.");
      return;
    }
    const payload = { name: name.trim(), description: description.trim() || null, cases: cases.map(toCaseInput) };
    try {
      if (!suite) {
        const created = await onCreate(payload);
        navigate(`/evaluations/suites/${created.id}`, { replace: true });
        return;
      }
      const saved = await onUpdate({ ...payload, baseVersion: suite.currentVersion });
      onNotice(
        saved.version > suite.version ? `Saved as version ${saved.version}.` : "Saved. The cases did not change, so no new version was made.",
      );
    } catch (err) {
      if (err instanceof ApiError && err.isConflict) {
        setSaveError("This suite was changed elsewhere since you opened it. Reload the page and edit again.");
      } else {
        setSaveError(err instanceof Error ? err.message : "Failed to save the suite.");
      }
    }
  };

  return (
    <Page
      title={suite ? suite.name : "New Eval Suite"}
      subtitle="Eval Cases and the Checks that decide whether an answer is acceptable"
      width="wide"
      leading={
        <Button iconOnly variant="ghost" aria-label="Back to Eval Suites" icon={<ArrowLeft />} onClick={() => navigate("/evaluations")} />
      }
      actions={
        <div className={styles.actions}>
          {suite && versions.length > 0 && (
            <Select
              aria-label="Version"
              controlSize="sm"
              value={String(suite.version)}
              onChange={(e) => showVersion(e.target.value)}
            >
              {versions.map((v) => (
                <option key={v.version} value={v.version}>
                  Version {v.version}
                  {v.version === suite.currentVersion ? " (current)" : ""}
                </option>
              ))}
            </Select>
          )}
          {suite && suite.cases.length > 0 && (
            <Button
              variant="secondary"
              icon={<Play size={15} />}
              title={`Evaluates saved version ${suite.version}; unsaved edits are not included.`}
              onClick={() => setEvaluateOpen(true)}
            >
              Evaluate
            </Button>
          )}
          {suite && suite.cases.length > 0 && (
            <Button
              variant="secondary"
              icon={<Columns3 size={15} />}
              title={`Compares several models on saved version ${suite.version}.`}
              onClick={() => setBenchmarkOpen(true)}
            >
              Benchmark
            </Button>
          )}
          {!readOnly && (
            <Button variant="primary" icon={<Check size={15} />} loading={isSaving} onClick={() => void save()}>
              {suite ? "Save" : "Create"}
            </Button>
          )}
        </div>
      }
    >
      {suite && (
        <StartEvaluationDialog
          open={evaluateOpen}
          suiteId={suite.id}
          suiteVersion={suite.version}
          onClose={() => setEvaluateOpen(false)}
        />
      )}
      {suite && (
        <StartBenchmarkDialog
          open={benchmarkOpen}
          suiteId={suite.id}
          suiteVersion={suite.version}
          onClose={() => setBenchmarkOpen(false)}
        />
      )}
      {readOnly && (
        <StatusMessage
          variant="info"
          message={`You are viewing version ${suite.version}, which is read-only. Pick the current version to edit.`}
        />
      )}
      {saveError && <StatusMessage variant="error" message={saveError} />}
      {notice && <StatusMessage variant="success" message={notice} />}

      <Card padding="none" as="section">
        <div className={styles.section}>
          <div className={styles.sectionGrid}>
            <FormField id="suite-name" label="Name">
              <Input id="suite-name" value={name} maxLength={120} disabled={readOnly} onChange={(e) => setName(e.target.value)} />
            </FormField>
          </div>
          <FormField id="suite-description" label="Description">
            <Textarea
              id="suite-description"
              rows={2}
              value={description}
              disabled={readOnly}
              onChange={(e) => setDescription(e.target.value)}
            />
          </FormField>
        </div>
      </Card>

      <div className={styles.editorLayout}>
        <Card padding="none" as="section" aria-label="Eval Cases">
          <CardHeader
            icon={<History />}
            title={`Cases (${cases.length})`}
            actions={
              !readOnly && (
                <Button size="sm" variant="ghost" icon={<Plus size={14} />} onClick={addCase}>
                  Add
                </Button>
              )
            }
          />
          <ul className={styles.caseNav}>
            {cases.map((c, i) => (
              <li key={c.key}>
                <button
                  type="button"
                  className={`${styles.caseNavItem} ${(errorsByKey.get(c.key)?.length ?? 0) > 0 ? styles.caseNavInvalid : ""}`}
                  aria-current={c.key === selectedKey}
                  onClick={() => setSelectedKey(c.key)}
                >
                  {caseLabel(c, i)}
                </button>
              </li>
            ))}
          </ul>
        </Card>

        <Card padding="none" as="section">
          {selected ? (
            <>
              <CardHeader
                icon={<FlaskConical />}
                title={caseLabel(selected, cases.indexOf(selected))}
                actions={
                  <>
                    {selected.id === null && <Badge tone="info">new</Badge>}
                    {!readOnly && (
                      <Button
                        size="sm"
                        variant="ghost"
                        icon={<Trash2 size={14} />}
                        onClick={() => removeCase(selected.key)}
                      >
                        Remove case
                      </Button>
                    )}
                  </>
                }
              />
              <EvalCaseEditor
                draft={selected}
                tools={tools}
                readOnly={readOnly}
                onChange={(next) => setCases((prev) => prev.map((c) => (c.key === next.key ? next : c)))}
              />
            </>
          ) : (
            <EmptyState
              icon={<FlaskConical />}
              title="No cases yet"
              hint="Add a case: a prompt and the Checks its answer must pass."
              action={
                !readOnly && (
                  <Button variant="primary" size="sm" icon={<Plus size={14} />} onClick={addCase}>
                    Add case
                  </Button>
                )
              }
            />
          )}
        </Card>
      </div>

      {!readOnly && allErrors.length > 0 && (
        <ul className={styles.errors} aria-label="Problems">
          {allErrors.map((e) => (
            <li key={e}>{e}</li>
          ))}
        </ul>
      )}
    </Page>
  );
}
