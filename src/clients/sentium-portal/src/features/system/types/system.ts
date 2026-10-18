export interface SystemMetrics {
  host: HostInfo;
  memory: MemoryInfo;
  cpu: CpuInfo;
  disks: DiskInfo[];
  process: ProcessInfo;
}

export interface HostInfo {
  machineName: string;
  osDescription: string;
  osArchitecture: string;
  processorCount: number;
  runtimeVersion: string;
  uptime: string;
}

export interface MemoryInfo {
  totalMb: number;
  usedMb: number;
  availableMb: number;
  memoryLoadPercent: number;
  gcHeapSizeMb: number;
  gcGen0Collections: number;
  gcGen1Collections: number;
  gcGen2Collections: number;
}

export interface CpuInfo {
  processorCount: number;
  processCpuPercent: number;
  architecture: string;
}

export interface DiskInfo {
  name: string;
  label: string;
  fileSystem: string;
  totalGb: number;
  availableGb: number;
  usedGb: number;
  usagePercent: number;
}

export interface ProcessInfo {
  id: number;
  name: string;
  workingSetMb: number;
  privateMemoryMb: number;
  threadCount: number;
  handleCount: number;
  startTime: string;
  cpuTime: string;
}
