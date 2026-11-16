# ADR-035: Claude API key entered in Settings and stored encrypted (011 follow-up)

- **Status**: Proposed (architect, 2026-11-16). Conditional on constitution **1.4.0** (proposed,
  owner ratification required; text in
  `specs/011-ai-composer-agent/followups/claude-key/plan.md`).
- **Context documents**: `specs/011-ai-composer-agent/followups/claude-key/{spec.md, plan.md,
  research.md, data-model.md, contracts/DELTA.md}`, ADR-020 D3 (Claude backend), ADR-022 (013
  backups), ADR-027 (018 LAN security).

## Context

011 takes the Claude key only from `ANTHROPIC_API_KEY` in the shell that starts Docker
(constitution 1.3.0 condition (2)). The owner decided on 2026-11-16 that the key can be entered in
the GUI and stored encrypted in the SQLite database. The API runs in a Linux container where
ASP.NET Core Data Protection does not encrypt its key ring at rest when a key directory is chosen
explicitly (research R-02); the app is reachable on the LAN without authentication until 018.

## Decision

- **D1 Encryption**: ASP.NET Core Data Protection, `IDataProtector` with purpose
  `StemStudio.Agent.ClaudeApiKey.v1`, application name `StemStudio.Api`; ciphertext, last four
  characters and save time in table `AgentSecrets`. No own cryptography, no new dependency.
- **D2 Key ring placement**: its own Docker volume `api-keys` at `/keys`, mounted only into `api`,
  directory `0700` for `$APP_UID`; non-Docker: per-user directory (`%LOCALAPPDATA%\StemStudio\keys`
  with DPAPI on Windows, XDG data directory on Linux/macOS). Never inside the library, the backups
  folder, the app or a git work tree (else storing is disabled, the API still starts). Never in a
  013 backup.
- **D3 Precedence**: `ANTHROPIC_API_KEY` wins over a stored key; the status shows the active
  source.
- **D4 Write-only API**: `GET` (status only), `PUT`, `DELETE /api/agent/settings/claude-key`
  (backend API 1.21.0); status exposes configured, source, state, last four, stored state, save
  time. Never the key in responses, problems, logs, events, pages or backups.
- **D5 Local-only**: key writes and the Claude test are accepted only from loopback or the `web`
  service with loopback host names; the Web enables the controls only on loopback page hosts. Hard
  boundary with 018's `127.0.0.1` binding.
- **D6 Failure mode**: an undecryptable ciphertext is `stored-unreadable` with "Enter the key
  again"; never a crash or a blocked start.
- **D7 Opt-in unchanged**: storing a key never selects Claude; the notice still applies; only
  **Test key** or a Claude run contacts Anthropic.

## Consequences

- Protects the key against a leaked database file, backups and exports, and against the worker's
  read-only library mount. Does **not** protect against someone who can read both the database and
  the key ring (full host access) or the process memory; stated in the help topic and manuals.
- Before 018 a hostile LAN client can overwrite or delete (not read) the stored key through the
  Web page with a forged host name; owner question Q1 offers binding the ports to `127.0.0.1` now.
- A restore on another computer or a lost key volume means entering the key again.
- One more volume to document; the technical manual says not to copy `api-keys` with backups.

## Alternatives considered

- Plain text in the database or a `.env` file: rejected (the constitution's purpose).
- SQLCipher for the whole database: new native dependency, changes 013, key still on the host.
- Data Protection keys in the database (`PersistKeysToDbContext`): ring and ciphertext together,
  both in every backup; rejected.
- `ProtectKeysWithCertificate` on Linux: the certificate's private key would sit on the same host
  with the same permissions; adds management without a real gain (YAGNI).
