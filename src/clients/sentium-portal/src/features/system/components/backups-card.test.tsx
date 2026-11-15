import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, within } from "@testing-library/react";
import { BackupsCard } from "./backups-card";
import * as useBackupsHook from "@/features/system/hooks/useBackups";
import type { BackupsOverview, BackupStatus } from "@/features/system/types/backup";

const status: BackupStatus = {
  sentiumVersion: "0.1.0",
  serviceStartedAt: "2026-11-01T00:00:00Z",
  destinationReady: true,
  destinationProblem: null,
  running: false,
  nextRunAt: "2026-11-16T03:00:00Z",
  lastRun: {
    startedAt: "2026-11-15T03:00:00Z",
    finishedAt: "2026-11-15T03:01:00Z",
    trigger: "scheduled",
    succeeded: true,
    archive: "sentium-20261115T030000Z.tar",
    error: null,
  },
  backups: [
    {
      name: "sentium-20261115T030000Z.tar",
      createdAt: "2026-11-15T03:00:00Z",
      sizeBytes: 5 * 1024 * 1024,
      encrypted: false,
    },
    { name: "sentium-20261114T030000Z.tar.age", createdAt: "2026-11-14T03:00:00Z", sizeBytes: 2048, encrypted: true },
  ],
  keepDaily: 7,
  keepWeekly: 4,
  encryptionEnabled: false,
};

const healthy: BackupsOverview = { configured: true, maxAgeHours: 36, health: "Healthy", healthDetails: null, status };

const mutate = vi.fn();

function mockHook(
  overview: BackupsOverview | undefined,
  start: Partial<{ isPending: boolean; error: Error | null }> = {},
) {
  vi.spyOn(useBackupsHook, "useBackups").mockReturnValue({
    overview,
    isLoading: false,
    error: null,
    start: { mutate, isPending: false, error: null, ...start },
  } as unknown as ReturnType<typeof useBackupsHook.useBackups>);
}

beforeEach(() => {
  mutate.mockReset();
});

describe("BackupsCard", () => {
  it("lists the verified backups with their sizes", () => {
    mockHook(healthy);
    render(<BackupsCard />);

    const items = screen.getAllByTestId("backup-item");
    expect(items).toHaveLength(2);
    expect(within(items[0]).getByText("5.0 MB")).toBeInTheDocument();
    expect(within(items[1]).getByLabelText("Encrypted")).toBeInTheDocument();
    expect(screen.getByTestId("backup-health")).toHaveTextContent("Healthy");
    expect(screen.getByTestId("backup-last-run")).toHaveTextContent(/succeeded/i);
  });

  it("starts a backup from 'Back up now'", () => {
    mockHook(healthy);
    render(<BackupsCard />);

    fireEvent.click(screen.getByTestId("backup-now"));
    expect(mutate).toHaveBeenCalledOnce();
  });

  it("disables the button while a backup runs", () => {
    mockHook({ ...healthy, status: { ...status, running: true } });
    render(<BackupsCard />);

    expect(screen.getByTestId("backup-now")).toBeDisabled();
    expect(screen.getByTestId("backup-last-run")).toHaveTextContent(/running now/i);
  });

  it("shows why backups need attention", () => {
    mockHook({
      ...healthy,
      health: "Unhealthy",
      healthDetails: "The newest backup is 40 hours old (limit 36).",
      status: { ...status, lastRun: { ...status.lastRun!, succeeded: false, error: "disk full" } },
    });
    render(<BackupsCard />);

    expect(screen.getByTestId("backup-health")).toHaveTextContent(/needs attention/i);
    expect(screen.getByText(/40 hours old/)).toBeInTheDocument();
    expect(screen.getByTestId("backup-last-run")).toHaveTextContent(/failed.*disk full/i);
  });

  it("explains when backups are not set up", () => {
    mockHook({ configured: false, maxAgeHours: 36, health: "Unknown", healthDetails: "Not set up.", status: null });
    render(<BackupsCard />);

    expect(screen.getByText(/backups are not set up/i)).toBeInTheDocument();
    expect(screen.getByTestId("backup-now")).toBeDisabled();
  });

  it("shows the error when a backup cannot start", () => {
    mockHook(healthy, { error: new Error("A backup is already running.") });
    render(<BackupsCard />);

    expect(screen.getByText("A backup is already running.")).toBeInTheDocument();
  });
});
