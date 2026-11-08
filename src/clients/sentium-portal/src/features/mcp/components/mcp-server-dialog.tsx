import { useState } from "react";
import { Check, Eye, EyeOff, Lock, LockOpen, Plus, Server, Trash2, Undo2 } from "lucide-react";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Input, Select, SwitchRow, Textarea } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { StatusMessage } from "@/shared/ui/status-message";
import type { CreateMcpServerPayload, McpServer, McpTransport, UpdateMcpServerPayload } from "../types/mcp";
import { describeError } from "../utils/errors";
import styles from "../mcp.module.css";

interface McpServerDialogProps {
  open: boolean;
  server?: McpServer | null;
  isSaving: boolean;
  onCreate: (payload: CreateMcpServerPayload) => Promise<unknown>;
  onUpdate: (id: string, payload: UpdateMcpServerPayload) => Promise<unknown>;
  onClose: () => void;
}

interface EntryRow {
  key: string;
  value: string;
  secret: boolean;
  reveal: boolean;
}

const FORM_ID = "mcp-server-form";

const blankToNull = (value: string) => (value.trim() === "" ? null : value.trim());
const numberOrNull = (value: string) => (value.trim() === "" ? null : Number(value));
const nonEmptyOrNull = <T,>(map: Record<string, T>) => (Object.keys(map).length > 0 ? map : null);

export function McpServerDialog({ open, server, isSaving, onCreate, onUpdate, onClose }: McpServerDialogProps) {
  const editing = Boolean(server);

  const [name, setName] = useState(server?.name ?? "");
  const [displayName, setDisplayName] = useState(server?.displayName ?? "");
  const [description, setDescription] = useState(server?.description ?? "");
  const [transport, setTransport] = useState<McpTransport>(server?.transport ?? "Http");
  const [endpoint, setEndpoint] = useState(server?.endpoint ?? "");
  const [image, setImage] = useState(server?.image ?? "");
  const [args, setArgs] = useState((server?.args ?? []).join("\n"));
  const [network, setNetwork] = useState(server?.network ?? "");
  const [memoryMb, setMemoryMb] = useState(server?.memoryMb?.toString() ?? "");
  const [cpuLimit, setCpuLimit] = useState(server?.cpuLimit?.toString() ?? "");
  const [enabled, setEnabled] = useState(server?.enabled ?? true);
  const [newEntries, setNewEntries] = useState<EntryRow[]>([]);
  const [variableValues, setVariableValues] = useState<Record<string, string>>(server?.variables ?? {});
  const [removedEntries, setRemovedEntries] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);

  const isHttp = transport === "Http";
  const secretNoun = isHttp ? "header" : "environment variable";

  const updateEntry = (index: number, patch: Partial<EntryRow>) =>
    setNewEntries((rows) => rows.map((r, i) => (i === index ? { ...r, ...patch } : r)));

  const collectEntries = () => {
    const secrets: Record<string, string | null> = {};
    const variables: Record<string, string | null> = {};
    const put = (entryName: string, value: string | null, secret: boolean) => {
      delete secrets[entryName];
      delete variables[entryName];
      (secret ? secrets : variables)[entryName] = value;
    };

    if (server) {
      for (const removed of removedEntries) {
        put(removed, null, !(removed in server.variables));
      }

      for (const [variable, value] of Object.entries(variableValues)) {
        if (!removedEntries.includes(variable) && server.variables[variable] !== value) {
          put(variable, value, false);
        }
      }
    }

    for (const entry of newEntries) {
      if (entry.key.trim() !== "") {
        put(entry.key.trim(), entry.value, entry.secret);
      }
    }

    return { secrets, variables };
  };

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setError(null);

    const argList = args
      .split("\n")
      .map((a) => a.trim())
      .filter(Boolean);

    const { secrets, variables } = collectEntries();

    try {
      if (server) {
        await onUpdate(server.id, {
          displayName: displayName.trim(),
          description: blankToNull(description),
          endpoint: isHttp ? blankToNull(endpoint) : null,
          image: isHttp ? null : blankToNull(image),
          args: isHttp ? null : argList,
          network: isHttp ? null : blankToNull(network),
          memoryMb: numberOrNull(memoryMb),
          cpuLimit: numberOrNull(cpuLimit),
          secrets: nonEmptyOrNull(secrets),
          variables: nonEmptyOrNull(variables),
          enabled,
        });
      } else {
        await onCreate({
          name: name.trim(),
          displayName: displayName.trim() || name.trim(),
          description: blankToNull(description),
          transport,
          endpoint: isHttp ? blankToNull(endpoint) : null,
          image: isHttp ? null : blankToNull(image),
          args: isHttp ? null : argList,
          network: isHttp ? null : blankToNull(network),
          memoryMb: numberOrNull(memoryMb),
          cpuLimit: numberOrNull(cpuLimit),
          secrets: secrets as Record<string, string>,
          variables: variables as Record<string, string>,
          enabled,
        });
      }

      onClose();
    } catch (err) {
      setError(describeError(err, "Could not save the MCP server."));
    }
  };

  const toggleRemoved = (entryName: string) =>
    setRemovedEntries((names) =>
      names.includes(entryName) ? names.filter((x) => x !== entryName) : [...names, entryName],
    );

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="lg"
      icon={<Server />}
      title={editing ? `Edit ${server?.displayName}` : "Register an MCP server"}
      description={
        editing
          ? "The name and transport are fixed. Secret values are write-only: leave them untouched to keep them."
          : "Sentium only connects to MCP servers on the local network. Tools stay unavailable until you sync and approve them."
      }
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" type="submit" form={FORM_ID} icon={<Check size={15} />} loading={isSaving}>
            {editing ? "Save" : "Register"}
          </Button>
        </>
      }
    >
      <form id={FORM_ID} className={styles.form} onSubmit={(e) => void submit(e)}>
        <div className={styles.formGrid}>
          <FormField
            id="mcp-name"
            label="Name"
            hint="Prefixes the tool names agents see (name__tool). Cannot change later."
          >
            <Input
              id="mcp-name"
              value={name}
              placeholder="home-assistant"
              disabled={editing}
              required
              pattern="[a-z0-9][a-z0-9\-]{1,23}"
              onChange={(e) => setName(e.target.value.toLowerCase())}
            />
          </FormField>
          <FormField id="mcp-display-name" label="Display name">
            <Input
              id="mcp-display-name"
              value={displayName}
              placeholder="Home Assistant"
              onChange={(e) => setDisplayName(e.target.value)}
            />
          </FormField>
        </div>

        <FormField id="mcp-description" label="Description">
          <Input
            id="mcp-description"
            value={description}
            placeholder="What this server gives agents access to"
            onChange={(e) => setDescription(e.target.value)}
          />
        </FormField>

        <FormField id="mcp-transport" label="Transport">
          <Select
            id="mcp-transport"
            value={transport}
            disabled={editing}
            onChange={(e) => setTransport(e.target.value as McpTransport)}
          >
            <option value="Http">HTTP - a Streamable HTTP server on the local network</option>
            <option value="SandboxStdio">Sandbox container - a stdio server run by the Sandbox</option>
          </Select>
        </FormField>

        {isHttp ? (
          <FormField
            id="mcp-endpoint"
            label="Endpoint"
            hint="Loopback or a private address (10.x, 172.16-31.x, 192.168.x, 100.64.x, fc00::/7). Checked again on every connect."
          >
            <Input
              id="mcp-endpoint"
              value={endpoint}
              placeholder="http://192.168.1.20:8123/mcp"
              required
              onChange={(e) => setEndpoint(e.target.value)}
            />
          </FormField>
        ) : (
          <>
            <FormField
              id="mcp-image"
              label="Image"
              hint="Pinned by digest and allowed by the Sandbox operator. Bake dependencies into the image: the container has no internet access."
            >
              <Input
                id="mcp-image"
                value={image}
                placeholder="mcp/time@sha256:…"
                required
                onChange={(e) => setImage(e.target.value)}
              />
            </FormField>
            <FormField id="mcp-args" label="Arguments" hint="One per line, passed to the image's entrypoint.">
              <Textarea id="mcp-args" mono rows={3} value={args} onChange={(e) => setArgs(e.target.value)} />
            </FormField>
            <div className={styles.formGrid3}>
              <FormField id="mcp-network" label="Network" hint="Empty = no network.">
                <Input
                  id="mcp-network"
                  value={network}
                  placeholder="none"
                  onChange={(e) => setNetwork(e.target.value)}
                />
              </FormField>
              <FormField id="mcp-memory" label="Memory (MB)">
                <Input
                  id="mcp-memory"
                  type="number"
                  min={32}
                  value={memoryMb}
                  placeholder="256"
                  onChange={(e) => setMemoryMb(e.target.value)}
                />
              </FormField>
              <FormField id="mcp-cpu" label="CPUs">
                <Input
                  id="mcp-cpu"
                  type="number"
                  min={0.1}
                  step={0.1}
                  value={cpuLimit}
                  placeholder="0.5"
                  onChange={(e) => setCpuLimit(e.target.value)}
                />
              </FormField>
            </div>
          </>
        )}

        <fieldset className={styles.secrets}>
          <legend className={styles.secretsLegend}>{isHttp ? "Headers" : "Environment variables"}</legend>
          <p className={styles.secretsHint}>
            Secret values are stored encrypted and never shown again. Unlock a value to store it as plain text you can
            read and edit here. Sent only to this server - every user&apos;s agents use the same credentials.
          </p>

          {server?.secretNames.map((secret) => {
            const removed = removedEntries.includes(secret);
            return (
              <div key={secret} className={`${styles.secretRow} ${styles.secretRowStored}`}>
                <code className={removed ? styles.secretRemoved : styles.secretName}>{secret}</code>
                <span className={styles.secretValue}>{removed ? "will be removed" : "•••••• (set)"}</span>
                <Button
                  variant="ghost"
                  size="sm"
                  iconOnly
                  aria-label={removed ? `Keep ${secret}` : `Remove ${secret}`}
                  icon={removed ? <Undo2 size={14} /> : <Trash2 size={14} />}
                  onClick={() => toggleRemoved(secret)}
                />
              </div>
            );
          })}

          {Object.keys(variableValues)
            .sort()
            .map((variable) => {
              const removed = removedEntries.includes(variable);
              return (
                <div key={variable} className={`${styles.secretRow} ${styles.secretRowStored}`}>
                  <code className={removed ? styles.secretRemoved : styles.secretName}>{variable}</code>
                  {removed ? (
                    <span className={styles.secretValue}>will be removed</span>
                  ) : (
                    <Input
                      aria-label={`Value of ${variable}`}
                      autoComplete="off"
                      value={variableValues[variable]}
                      onChange={(e) => setVariableValues((values) => ({ ...values, [variable]: e.target.value }))}
                    />
                  )}
                  <Button
                    variant="ghost"
                    size="sm"
                    iconOnly
                    aria-label={removed ? `Keep ${variable}` : `Remove ${variable}`}
                    icon={removed ? <Undo2 size={14} /> : <Trash2 size={14} />}
                    onClick={() => toggleRemoved(variable)}
                  />
                </div>
              );
            })}

          {newEntries.map((entry, index) => (
            <div key={index} className={styles.secretRow}>
              <Input
                aria-label={`New ${secretNoun} name`}
                value={entry.key}
                placeholder={isHttp ? "Authorization" : "API_KEY"}
                onChange={(e) => updateEntry(index, { key: e.target.value })}
              />
              <div className={styles.secretValueField}>
                <Input
                  aria-label={`New ${secretNoun} value`}
                  type={entry.secret && !entry.reveal ? "password" : "text"}
                  autoComplete="off"
                  value={entry.value}
                  placeholder={isHttp ? "Bearer …" : "value"}
                  onChange={(e) => updateEntry(index, { value: e.target.value })}
                />
                {entry.secret && (
                  <Button
                    variant="ghost"
                    size="sm"
                    iconOnly
                    aria-label={entry.reveal ? `Hide new ${secretNoun} value` : `Show new ${secretNoun} value`}
                    icon={entry.reveal ? <EyeOff size={14} /> : <Eye size={14} />}
                    onClick={() => updateEntry(index, { reveal: !entry.reveal })}
                  />
                )}
              </div>
              <Button
                variant="ghost"
                size="sm"
                iconOnly
                aria-label={
                  entry.secret
                    ? `Secret - store this ${secretNoun} as plain text`
                    : `Plain text - make this ${secretNoun} secret`
                }
                title={
                  entry.secret
                    ? "Secret: stored encrypted, never shown again"
                    : "Plain text: stored as-is, visible here"
                }
                icon={entry.secret ? <Lock size={14} /> : <LockOpen size={14} />}
                onClick={() => updateEntry(index, { secret: !entry.secret, reveal: false })}
              />
              <Button
                variant="ghost"
                size="sm"
                iconOnly
                aria-label={`Discard new ${secretNoun}`}
                icon={<Trash2 size={14} />}
                onClick={() => setNewEntries((rows) => rows.filter((_, i) => i !== index))}
              />
            </div>
          ))}

          <Button
            variant="ghost"
            size="sm"
            icon={<Plus size={14} />}
            onClick={() => setNewEntries((rows) => [...rows, { key: "", value: "", secret: true, reveal: false }])}
          >
            {editing ? `Add or replace a ${secretNoun}` : `Add a ${secretNoun}`}
          </Button>
        </fieldset>

        <SwitchRow
          label="Enabled"
          description="Disabled servers keep their configuration but offer no tools."
          checked={enabled}
          onChange={(e) => setEnabled(e.target.checked)}
        />

        {error && <StatusMessage variant="error" message={error} />}
      </form>
    </Dialog>
  );
}
