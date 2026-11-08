import { useState } from "react";
import { Check, SlidersHorizontal } from "lucide-react";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { Input, SwitchRow } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { StatusMessage } from "@/shared/ui/status-message";
import { useMcpSettings } from "../hooks/useMcpServers";
import type { McpSettings } from "../types/mcp";
import { describeError } from "../utils/errors";
import styles from "../mcp.module.css";

export function McpSettingsCard() {
  const { settings, isLoading, save, isSaving, isSaved, saveError } = useMcpSettings();

  if (isLoading || !settings) {
    return null;
  }

  return (
    <McpSettingsForm
      key={JSON.stringify(settings)}
      initial={settings}
      onSave={save}
      isSaving={isSaving}
      isSaved={isSaved}
      saveError={saveError}
    />
  );
}

interface McpSettingsFormProps {
  initial: McpSettings;
  onSave: (settings: McpSettings) => Promise<unknown>;
  isSaving: boolean;
  isSaved: boolean;
  saveError: unknown;
}

function McpSettingsForm({ initial, onSave, isSaving, isSaved, saveError }: McpSettingsFormProps) {
  const [form, setForm] = useState(initial);

  const number = (key: keyof Omit<McpSettings, "enabled">) => ({
    value: String(form[key]),
    onChange: (e: React.ChangeEvent<HTMLInputElement>) => setForm((f) => ({ ...f, [key]: Number(e.target.value) })),
  });

  return (
    <Card as="section" padding="none">
      <CardHeader
        icon={<SlidersHorizontal />}
        title="MCP settings"
        description="Apply to every MCP server and every user."
        actions={
          <Button
            size="sm"
            variant="primary"
            icon={<Check size={14} />}
            loading={isSaving}
            onClick={() => void onSave(form)}
          >
            Save
          </Button>
        }
      />
      <div className={styles.settings}>
        <SwitchRow
          label="MCP tools enabled"
          description="Kill switch: when off, no agent is offered an MCP tool and open connections are closed."
          checked={form.enabled}
          onChange={(e) => setForm((f) => ({ ...f, enabled: e.target.checked }))}
        />
        <div className={styles.formGrid4}>
          <FormField id="mcp-connect-timeout" label="Connect timeout (s)">
            <Input id="mcp-connect-timeout" type="number" min={1} max={300} {...number("connectTimeoutSeconds")} />
          </FormField>
          <FormField id="mcp-call-timeout" label="Call timeout (s)">
            <Input id="mcp-call-timeout" type="number" min={1} max={600} {...number("callTimeoutSeconds")} />
          </FormField>
          <FormField id="mcp-idle-timeout" label="Close idle after (min)">
            <Input id="mcp-idle-timeout" type="number" min={1} max={1440} {...number("idleTimeoutMinutes")} />
          </FormField>
          <FormField id="mcp-max-result" label="Max result (chars)">
            <Input id="mcp-max-result" type="number" min={1000} max={200000} {...number("maxResultChars")} />
          </FormField>
        </div>
        {saveError != null && (
          <StatusMessage variant="error" message={describeError(saveError, "Could not save the MCP settings.")} />
        )}
        {isSaved && saveError == null && <StatusMessage variant="success" message="Saved." />}
      </div>
    </Card>
  );
}
