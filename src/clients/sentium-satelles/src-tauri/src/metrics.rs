//! Reads the PC's health for a Report: machine, OS, uptime, system-wide CPU, memory and fixed disks. Nothing that
//! describes what the person is doing. Field lengths are trimmed to what the server accepts.

use sysinfo::{Disks, System};

use crate::api::{DeviceReport, DiskReport};

const MACHINE_NAME_MAX: usize = 64;
const OS_DESCRIPTION_MAX: usize = 128;
const DISK_NAME_MAX: usize = 32;
const MAX_DISKS: usize = 32;

pub struct Metrics {
    /// `None` when the platform refused to open system information; Reports then carry zeros for CPU and memory.
    system: Option<System>,
}

impl Metrics {
    /// Takes the first CPU sample, so the first Report measures from app start.
    pub fn new() -> Self {
        let mut system = System::new()
            .inspect_err(|e| log::warn!("system information unavailable: {e}"))
            .ok();
        if let Some(system) = system.as_mut() {
            system.refresh_cpu_usage();
        }
        Self { system }
    }

    /// CPU use is measured since the previous call.
    pub fn collect(&mut self, app_version: &str) -> DeviceReport {
        let (cpu, total, available) = match self.system.as_mut() {
            Some(system) => {
                system.refresh_cpu_usage();
                system.refresh_memory();
                (
                    f64::from(system.global_cpu_usage()),
                    system.total_memory(),
                    system.available_memory(),
                )
            }
            None => (0.0, 0, 0),
        };

        DeviceReport {
            machine_name: trim(
                System::host_name().unwrap_or_else(|_| "unknown".into()),
                MACHINE_NAME_MAX,
            ),
            os_description: trim(
                System::long_os_version().unwrap_or_else(|_| std::env::consts::OS.into()),
                OS_DESCRIPTION_MAX,
            ),
            uptime_seconds: System::uptime().unwrap_or_default(),
            app_version: app_version.into(),
            cpu_percent: cpu.clamp(0.0, 100.0),
            memory_total_bytes: total,
            memory_available_bytes: available.min(total),
            disks: fixed_disks(),
        }
    }
}

fn fixed_disks() -> Vec<DiskReport> {
    let Ok(disks) = Disks::new_with_refreshed_list() else {
        return Vec::new();
    };

    let mut reports: Vec<DiskReport> = Vec::new();
    for disk in disks
        .iter()
        .filter(|d| !d.is_removable() && d.total_space() > 0)
    {
        let mount = disk.mount_point().to_string_lossy();
        let name = trim(
            mount.trim_end_matches(['\\', '/']).to_string(),
            DISK_NAME_MAX,
        );
        let name = if name.is_empty() {
            "/".to_string()
        } else {
            name
        };

        if reports.len() < MAX_DISKS && !reports.iter().any(|r| r.name == name) {
            reports.push(DiskReport {
                name,
                total_bytes: disk.total_space(),
                available_bytes: disk.available_space().min(disk.total_space()),
            });
        }
    }

    reports
}

fn trim(value: String, max: usize) -> String {
    if value.chars().count() <= max {
        value
    } else {
        value.chars().take(max).collect()
    }
}
