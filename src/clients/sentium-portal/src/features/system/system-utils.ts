export function formatUptime(uptime: string): string {
  const parts = uptime.split(":");
  if (parts.length < 3) {
    return uptime;
  }

  let days = 0;
  let hours = parseInt(parts[0], 10);
  const minutes = parseInt(parts[1], 10);

  if (parts[0].includes(".")) {
    const dp = parts[0].split(".");
    days = parseInt(dp[0], 10);
    hours = parseInt(dp[1], 10);
  }

  const segments: string[] = [];
  if (days > 0) {
    segments.push(`${days}d`);
  }
  if (hours > 0) {
    segments.push(`${hours}h`);
  }
  segments.push(`${minutes}m`);
  return segments.join(" ");
}

export function formatMb(mb: number): string {
  if (mb >= 1024) {
    return `${(mb / 1024).toFixed(1)} GB`;
  }
  return `${mb.toFixed(0)} MB`;
}

export function formatGb(gb: number): string {
  if (gb >= 1024) {
    return `${(gb / 1024).toFixed(1)} TB`;
  }
  return `${gb.toFixed(1)} GB`;
}
