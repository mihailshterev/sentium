// The app window. All logic lives in Rust; this only collects input and shows the state.
const { invoke } = window.__TAURI__.core;
const { listen } = window.__TAURI__.event;

const $ = (id) => document.getElementById(id);

const RELATIVE_TIME_REFRESH_MS = 30_000;

let lastState = null;

function render(state) {
  lastState = state;

  $("pair-panel").hidden = state.paired;
  $("paired-panel").hidden = !state.paired;
  $("status").textContent = state.statusText;
  $("status").dataset.status = state.status;

  if (!state.paired && !$("server").value && state.lastServer) {
    $("server").value = state.lastServer;
  }

  renderVault(state.paired, state.vault);
}

function renderVault(paired, vault) {
  $("vault-panel").hidden = !paired;
  const configured = Boolean(vault.folder);
  $("vault-none").hidden = configured;
  $("vault-set").hidden = !configured;

  if (!configured) {
    return;
  }

  $("vault-name").textContent = vault.name;
  $("vault-folder").textContent = vault.folder;
  $("vault-status").textContent = vault.statusText;
  $("vault-status").dataset.status = vault.state;
  $("vault-synced").textContent = vault.lastSyncMs ? `Last synced ${ago(vault.lastSyncMs)}.` : "";
  $("vault-pause").textContent = vault.state === "Paused" ? "Resume" : "Pause";
  $("vault-takeover").hidden = vault.state !== "HeldElsewhere";
  $("vault-sync").disabled = vault.state === "Paused" || vault.state === "HeldElsewhere";

  // Never overwrite exclusions the person is still editing.
  if (document.activeElement !== $("vault-excludes")) {
    $("vault-excludes").value = vault.excludes.join("\n");
  }
}

function ago(ms) {
  const minutes = Math.round((Date.now() - ms) / 60_000);
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  return hours < 24 ? `${hours} h ago` : new Date(ms).toLocaleString();
}

async function pair(event) {
  event.preventDefault();
  const button = $("pair");
  $("error").textContent = "";
  button.disabled = true;
  button.textContent = "Pairing…";

  try {
    await invoke("pair", { server: $("server").value, code: $("code").value, name: $("name").value });
    $("code").value = "";
  } catch (error) {
    $("error").textContent = String(error);
  } finally {
    button.disabled = false;
    button.textContent = "Pair";
  }
}

/** Runs a vault command, showing its error under the vault panel. */
async function vaultCommand(command, args) {
  $("vault-error").textContent = "";
  try {
    await invoke(command, args);
  } catch (error) {
    $("vault-error").textContent = String(error);
  }
}

$("pair-form").addEventListener("submit", pair);
$("close").addEventListener("click", () => invoke("hide_window"));

$("vault-choose").addEventListener("click", () => vaultCommand("choose_vault_folder"));
$("vault-change").addEventListener("click", () => vaultCommand("choose_vault_folder"));
$("vault-sync").addEventListener("click", () => vaultCommand("sync_vault_now"));
$("vault-takeover").addEventListener("click", () => vaultCommand("take_over_vault"));
$("vault-stop").addEventListener("click", () => vaultCommand("stop_vault_sync"));
$("vault-save").addEventListener("click", () => vaultCommand("set_vault_excludes", { excludes: $("vault-excludes").value }));
$("vault-pause").addEventListener("click", () =>
  vaultCommand("set_vault_paused", { paused: lastState?.vault.state !== "Paused" }),
);

setInterval(() => lastState && renderVault(lastState.paired, lastState.vault), RELATIVE_TIME_REFRESH_MS);

await listen("satelles-state", (event) => render(event.payload));
render(await invoke("get_state"));
$("name").value = await invoke("default_device_name");
