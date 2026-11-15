// The pairing window. All logic lives in Rust; this only collects the server address and code and shows the state.
const { invoke } = window.__TAURI__.core;
const { listen } = window.__TAURI__.event;

const $ = (id) => document.getElementById(id);

function render(state) {
  $("pair-panel").hidden = state.paired;
  $("paired-panel").hidden = !state.paired;
  $("status").textContent = state.statusText;
  $("status").dataset.status = state.status;

  if (!state.paired && !$("server").value && state.lastServer) {
    $("server").value = state.lastServer;
  }
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

$("pair-form").addEventListener("submit", pair);
$("close").addEventListener("click", () => invoke("hide_window"));

await listen("satelles-state", (event) => render(event.payload));
render(await invoke("get_state"));
$("name").value = await invoke("default_device_name");
