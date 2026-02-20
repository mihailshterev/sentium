import { useState, useRef } from "react";
import Markdown from "react-markdown";

interface LogEntry {
  Author: string;
  Text: string;
}

export default function AgentTerminal() {
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [status, setStatus] = useState<"idle" | "running">("idle");
  const scrollRef = useRef<HTMLDivElement>(null);

  // useEffect(() => {
  //   if (scrollRef.current) {
  //     scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
  //   }
  // }, [logs]);

  const scenarios = [
    {
      id: "iot_to_nas_ransom_chain",
      label: "IoT Foothold → NAS Ransomware Chain",
      data: {
        activity:
          "Compromised smart light hub begins scanning local subnet, brute-forces NAS credentials over SMB, followed by rapid file encryption and outbound traffic to known ransomware C2.",
        user: "smart_hub",
        system_context: "IoT VLAN → Synology NAS",
      },
    },
    {
      id: "router_dns_takeover",
      label: "Router DNS Hijack & Credential Harvesting",
      data: {
        activity:
          "Home router admin login from internal IP, DNS servers changed to suspicious resolver, followed by multiple successful logins to banking and email accounts from foreign IPs.",
        user: "home_router",
        system_context: "Gateway",
      },
    },
    {
      id: "external_bruteforce_vpn_pivot",
      label: "VPN Brute Force → Internal Pivot",
      data: {
        activity:
          "300+ failed OpenVPN login attempts from external IP, one successful login, followed by SSH connections to home server and large outbound archive upload to cloud storage.",
        user: "home_vpn_user",
        system_context: "VPN Gateway → Linux Home Server",
      },
    },
    {
      id: "arp_mitm_session_hijack",
      label: "ARP Spoofing & Session Hijack",
      data: {
        activity:
          "Unknown device joins WiFi late at night, duplicate ARP responses detected for gateway, users report HTTPS certificate warnings, followed by unauthorized email password reset.",
        user: "unknown_device",
        system_context: "Home WiFi LAN",
      },
    },
    {
      id: "stealth_powershell_persistence",
      label: "Phishing → PowerShell Persistence",
      data: {
        activity:
          "User opens email attachment, spawns obfuscated PowerShell process, creates scheduled task for persistence, establishes encrypted outbound tunnel and begins periodic data exfiltration.",
        user: "family_laptop",
        system_context: "Windows 11 Desktop",
      },
    },
  ];

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const runAgent = async (scenarioData?: any) => {
    setLogs([]);
    setStatus("running");

    const eventSource = new EventSource(
      "https://localhost:7127/agents/stream/events.network.scan",
    );

    eventSource.onmessage = (e) => {
      if (!e.data || e.data === "null") return;
      try {
        const data = JSON.parse(e.data);
        const author = data.Author || data.author || "Agent";
        const text = data.Text || data.text || "";

        if (text) {
          setLogs((prev) => {
            if (prev.length > 0 && prev[prev.length - 1].Author === author) {
              const updatedLogs = [...prev];
              updatedLogs[updatedLogs.length - 1] = {
                ...updatedLogs[updatedLogs.length - 1],
                Text: updatedLogs[updatedLogs.length - 1].Text + text,
              };
              return updatedLogs;
            }
            return [...prev, { Author: author, Text: text }];
          });
        }
      } catch (err) {
        console.error(err);
      }
    };

    await new Promise((resolve) => setTimeout(resolve, 600));

    await fetch("https://localhost:7127/agents/test-pipeline", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(
        scenarioData || { activity: "Default scan", user: "system" },
      ),
    });

    eventSource.onerror = () => {
      eventSource.close();
      setStatus("idle");
    };
  };

  const getRoleClass = (role: string) => {
    const r = role.toLowerCase();
    if (r.includes("security")) return "role-security";
    if (r.includes("summarizer")) return "role-summarizer";
    if (r.includes("forensics")) return "role-forensics";
    if (r.includes("intel")) return "role-intel";
    return "role-system";
  };

  return (
    <div className="terminal-container">
      <div className="terminal-card">
        <header className="terminal-header">
          <div>
            <h2 className="terminal-title">SENTIUM // AGENT_RUNTIME_LOGS</h2>
            <small style={{ color: "#52525b" }}>v1.0.0 // SSE_ENABLED</small>
          </div>
          <div className="scenario-bar">
            {scenarios.map((s) => (
              <button
                key={s.id}
                className="scenario-btn"
                disabled={status === "running"}
                onClick={() => runAgent(s.data)}
              >
                {s.label}
              </button>
            ))}
          </div>
          <button
            className="run-button"
            onClick={() => runAgent()}
            disabled={status === "running"}
          >
            {status === "running" ? "Executing..." : "Default Scan"}
          </button>
        </header>

        <div className="log-window" ref={scrollRef}>
          {logs.map((log, i) => (
            <div key={i} className="log-entry">
              <span className={`role-label ${getRoleClass(log.Author)}`}>
                [{log.Author.toUpperCase()}]
              </span>
              <div className="text-content">
                <Markdown>{log.Text}</Markdown>
              </div>
            </div>
          ))}
          {status === "running" && <span className="cursor" />}
        </div>
      </div>
    </div>
  );
}
