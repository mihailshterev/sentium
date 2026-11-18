# scripts

Developer scripts. Run them from any directory with bash (Git Bash on Windows): `bash scripts/<name>.sh`, or
`scripts/<name>.sh` where the executable bit is set. Each script prints its usage with an unknown argument.

| Script                                 | What it does                                                                                                                                                                            |
| -------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [setup.sh](setup.sh)                   | First-run bootstrap: checks prerequisites, creates `.env` with random secrets, installs the portal, Satelles and e2e dependencies. Safe to re-run.                                      |
| [verify.sh](verify.sh)                 | Runs the CI checks locally: .NET build (zero warnings) + unit tests, portal lint/build/tests. `--integration` adds the Docker-backed tests, `--satelles` the Rust checks.               |
| [add-migration.sh](add-migration.sh)   | `add-migration.sh <Module> <Name>` adds an EF Core migration to that module's DbContext, then runs `MigrationSnapshotTests`. `--list` shows the modules with a DbContext.               |
| [trust-lan-cert.sh](trust-lan-cert.sh) | Exports the caddy root CA from the compose stack, prints its fingerprint and trusts it on this machine (Windows, macOS, Linux). `--export-only` just writes the file for other devices. |

Scripts that run inside containers stay next to their service in `infra/` (for example
`infra/backup/restore.sh`, `infra/llm/fetch-models.sh`).
