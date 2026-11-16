# Feature Specification: Claude API key in Settings, stored encrypted (011 follow-up "claude-key")

**Feature**: follow-up of `specs/011-ai-composer-agent` (AI composer agent)
**Branch**: `011-claude-key-spec` (design), implementation branch `011-claude-key`
**Created**: 2026-11-16
**Status**: Draft (architect). Implementation is blocked until the owner ratifies the constitution
amendment 1.4.0 proposed in [plan.md](plan.md#constitution-amendment-140-proposed-owner-ratification-required).
**Input**: owner decision 2026-11-16: "enter the Anthropic API key in the GUI (Settings page) and
store it ENCRYPTED in the SQLite database, instead of only via the `ANTHROPIC_API_KEY` environment
variable". Binding design agreed by the coordinator with the owner (Data Protection, separate key
volume, write-only API, environment wins, local-only entry, honest threat model).

## Why a follow-up folder and not a new feature number

This work changes one requirement of feature 011 (FR-018: "the key MUST come only from the
environment"), two of its contracts (backend API `agent` tag, UI test ids) and the 011 Settings
page. It adds no new capability to the roadmap. A new `specs/0NN-*` number would collide with the
roadmap numbering (012-021 are allocated and designed; 022 would suggest a new backlog feature) and
Spec Kit's prefix lookup of the branch `011-claude-key*` already resolves to `specs/011-*`. The
follow-up therefore lives in `specs/011-ai-composer-agent/followups/claude-key/` with the full
Spec Kit set (spec, plan, research, data-model delta, contracts delta, quickstart, tasks) and is
merged into the 011 documents only after ratification (tasks T001, T053).

## Clarifications

### Session 2026-11-16 (architect, within the binding design)

- Q: What does "write-only" mean exactly? -> A: no endpoint, event, log line, problem detail,
  exception message, page or backup README ever contains the key. Only four facts are visible:
  whether a key is usable, which source is active (`environment` or `stored`), the **last four
  characters** of the active key (so the owner can recognise which key it is) and when the stored
  key was saved. Showing the last four characters is new and is part of the amendment text.
- Q: Which source wins when both exist? -> A: the environment variable, always (owner design). The
  stored key is kept and shown as "saved, not in use while ANTHROPIC_API_KEY is set".
- Q: Does saving a key select Claude? -> A: no. Ollama stays the default; the owner still selects
  Claude and acknowledges the paid-service notice (011 FR-019). Deleting the stored key does not
  switch the backend either; the status then explains that Claude has no key and offers "Switch to
  Local".
- Q: When is Anthropic contacted? -> A: only when the owner presses **Test key** (free model
  lookup, as 011's connection test), or when Claude is the selected backend and a command is sent.
  Saving never contacts Anthropic.
- Q: What if the stored key cannot be decrypted (database restored on another machine, key volume
  lost or recreated)? -> A: status `stored-unreadable` with "Enter the key again"; the app, the
  agent with Ollama and every other feature keep working; no crash, no retry loop.
- Q: From where can the key be entered? -> A: only from the computer StemStudio runs on
  (localhost). Paired 018 remote devices never can. Before 018 is built the Docker set-up cannot
  tell a browser on the computer from one on the LAN (research R-05); the rule is then a host-name
  guard, and the hard boundary arrives with 018's `127.0.0.1` binding (owner question Q1 in
  plan.md).
- Q: Is the ciphertext in a 013 backup? -> A: yes, inside the database snapshot (it is useless
  without the key ring, which a backup never contains). Restoring on the same installation keeps
  the key working; on another installation the status becomes `stored-unreadable` (owner question
  Q2 in plan.md offers the stricter alternative).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Enter the Claude key in Settings (Priority: P1) MVP

On the Settings page, in the Claude part of the agent section, the owner pastes the Anthropic API
key into a password field and presses **Save key**. The field is cleared at once; the status shows
"Key saved (ends with ...WXYZ), stored encrypted on this computer". Claude can now be selected
(after acknowledging the paid-service notice as before) without exporting `ANTHROPIC_API_KEY`.

**Why this priority**: the owner's request; setting an environment variable in the shell that
starts Docker is awkward and is lost with every new shell.

**Independent Test**: fake stack without `ANTHROPIC_API_KEY`; save a sentinel key through the UI;
the status says `stored` with the last four; the SQLite file, the logs, every API response and the
page DOM do not contain the sentinel (byte scan); selecting Claude then works against the stub
Anthropic handler.

**Acceptance Scenarios**:

1. **Given** no key in the environment and none stored, **When** the owner opens Settings, **Then**
   the Claude key status reads "No key" and offers the key field and **Save key**; the 011 text
   "set ANTHROPIC_API_KEY ..." is shown as the alternative.
2. **Given** a well-formed key in the field, **When** the owner presses **Save key**, **Then** the
   key is stored encrypted, the field is emptied, the status shows `stored`, "ends with" the last
   four characters and the time saved, and no request goes to Anthropic.
3. **Given** a malformed value (no `sk-ant-` prefix, wrong length, spaces inside, an admin key),
   **When** the owner saves, **Then** a readable error explains the expected format **without
   repeating the value**, and nothing is stored.
4. **Given** a key was saved, **When** the API restarts or the containers are rebuilt
   (`docker compose up --build`), **Then** the stored key is still readable (stable key ring and
   application name).
5. **Given** a stored key and backend Ollama, **Then** the backend stays Ollama; Claude is only
   used after the owner selects it and acknowledges the notice.

---

### User Story 2 - See which key is in use (Priority: P1)

The status line always tells the owner which key StemStudio would use: none, the environment
variable, or the stored key, each with the last four characters; when both exist it says the
environment variable wins.

**Why this priority**: without it, two key sources are confusing and a wrong key is hard to find.

**Independent Test**: API tests for the four combinations (env yes/no x stored yes/no) plus
"stored but unreadable"; bUnit tests for the rendered status texts.

**Acceptance Scenarios**:

1. **Given** `ANTHROPIC_API_KEY` is set and no key is stored, **Then** the status shows
   "From the environment (ANTHROPIC_API_KEY), ends with ...ABCD" and the field to store a key is
   still offered (it would be used when the variable is removed).
2. **Given** both exist, **Then** the status shows the environment key as active and the stored key
   as "saved on <date>, ends with ...WXYZ, not in use while ANTHROPIC_API_KEY is set".
3. **Given** only a stored key, **Then** the status shows "Stored key, ends with ...WXYZ, saved on
   <date>".
4. **Given** any state, **Then** `GET /api/agent/status` and `GET /api/agent/settings/claude-key`
   return only `configured`, `source`, `state`, `lastFour`, `stored`, `storedAt`; never the key.

---

### User Story 3 - Replace or delete the stored key (Priority: P2)

The owner can replace the stored key (new key from the Anthropic console, or a key that leaked)
and delete it.

**Independent Test**: API and bUnit tests; E2E on the fake stack: save, replace, delete; the
database no longer holds a ciphertext row after delete.

**Acceptance Scenarios**:

1. **Given** a stored key, **When** the owner presses **Replace key**, enters a new key and saves,
   **Then** the old ciphertext is overwritten in the same transaction and the status shows the new
   last four.
2. **Given** a stored key, **When** the owner presses **Delete key** and confirms, **Then** the row
   is removed; with no environment key the status becomes "No key"; when Claude was the selected
   backend, the agent shows "Claude has no key" with "Switch to Local" (no automatic switch).
3. **Given** a Claude run is active, **When** the key is replaced or deleted, **Then** the change
   applies to the next command; the panel shows "Key changed - applies to the next command" and the
   owner can cancel the active run.
4. **Given** no stored key, **When** delete is requested through the API, **Then** it answers 200
   with the unchanged status (idempotent).

---

### User Story 4 - Test the key on request (Priority: P2)

The owner presses **Test key** to check that Anthropic accepts the key, without spending tokens.

**Independent Test**: stub Anthropic handler (200, 401, 403, timeout, network error); a "must not be
called" handler proves that saving, loading Settings, start-up and status polling make **0**
requests.

**Acceptance Scenarios**:

1. **Given** a usable key, **When** the owner presses **Test key**, **Then** the free model lookup
   runs once and the result "Anthropic accepted the key (412 ms)" or a readable failure ("Anthropic
   rejected the key (401). Replace it." / "Anthropic not reachable") appears within 10 s.
2. **Given** both keys exist, **When** the owner chooses "Test the stored key", **Then** the stored
   key is tested even though the environment key is active.
3. **Given** any state, **Then** no other action (save, replace, delete, page load, start-up,
   status refresh) contacts Anthropic.

---

### User Story 5 - Recover from an unreadable stored key (Priority: P2)

After restoring a backup on a new computer, recreating the key volume or deleting the key ring
directory, the stored key can no longer be decrypted. StemStudio says so and tells the owner how
to fix it.

**Independent Test**: API test with a ciphertext produced by a different key ring (or a corrupted
ciphertext) -> status `stored-unreadable`, the API starts, every other endpoint works, Claude run
attempts fail with the readable reason; saving a new key replaces the unreadable row.

**Acceptance Scenarios**:

1. **Given** an unreadable stored key and no environment key, **Then** the status reads "The stored
   key cannot be read on this installation (the encryption keys are missing or different, e.g. after
   a restore on another computer). Enter the key again." and `configured` is false.
2. **Given** that state and backend Claude, **When** a command is sent, **Then** it fails with the
   same reason and "Switch to Local"; nothing crashes; the failure is logged once per API start as
   a fixed event without exception message or ciphertext.
3. **Given** that state, **When** the owner saves a key, **Then** it is encrypted with the current
   key ring and the status becomes `stored`.
4. **Given** that state and an environment key, **Then** the environment key is used and the stored
   key is shown as unreadable with the same fix.

---

### User Story 6 - Key entry only on the computer itself (Priority: P1)

Writing, replacing, deleting and testing the stored key is possible only from StemStudio opened on
the computer it runs on, never from another device on the network and never from a paired 018
remote.

**Independent Test**: API tests with a non-loopback peer address and with a forbidden host name ->
`403 local-only`; bUnit test: on a page opened under a non-loopback host name the key field is
disabled with the explanation; 018 remote listener test (when 018 exists): the key paths are 404.

**Acceptance Scenarios**:

1. **Given** StemStudio is opened as `http://localhost:8080` on the computer, **Then** the key
   section is usable.
2. **Given** StemStudio is opened from another device (`http://<computer-ip>:8080`), **Then** the
   key section shows "The Claude key can only be entered on the computer StemStudio runs on" and
   its controls are disabled; a direct API call is refused with `403 local-only`.
3. **Given** feature 018 is active, **Then** remote devices have no command, page or path for the
   key (remote listener answers 404 for every API path, 018 FR-013).

### Edge Cases

- **Whitespace around a pasted key**: leading/trailing whitespace and line breaks are trimmed;
  whitespace inside the key is a format error.
- **Very long input / large body**: bodies over 1 KiB are refused (413) before parsing; the value is
  never logged.
- **Invalid JSON**: 400 with a fixed problem detail; no parser message that could quote the body.
- **Key ring directory missing, not writable or inside the library**: storing is disabled with the
  reason ("The key store is not available: <reason>. The environment variable still works.");
  reading an existing ciphertext reports `stored-unreadable`; the API still starts.
- **Key ring directory permissions wider than owner-only (Linux)**: the API tightens them to 0700
  at start when it owns the directory, otherwise it logs a warning once and continues.
- **Key ring rotation (90 days)**: retired keys stay in the ring, so older ciphertexts remain
  readable; when the stored key was protected with a retired key it is re-protected with the
  current key on the next successful read (no owner action).
- **Concurrent saves** (two tabs): last write wins inside one transaction; the response of each
  carries the resulting status.
- **Environment variable set to whitespace**: treated as unset (011 behaviour).
- **Browser password managers**: the field uses `autocomplete="off"` and is never pre-filled; the
  page never renders a stored value.
- **Database restored by 013 on the same installation**: the key ring is unchanged, so the key
  stays readable. **Merge restore (013 US5)** never imports the archive's secret row.
- **Fake and none backends, CI**: CI never stores a real key; tests use a sentinel built at run time
  (the repository secret scan forbids literal key-shaped strings).

## Requirements *(mandatory)*

### Functional Requirements

Numbering: `CK-FR-nnn`. On ratification these replace the key sentence of 011 FR-018 and extend
FR-019/FR-020 and SC-006 (task T053).

**Storage and sources**

- **CK-FR-001**: The system MUST accept a Claude API key from two sources: the `ANTHROPIC_API_KEY`
  environment variable of the API (unchanged) and a key stored through Settings. When both are
  present the environment variable MUST be used.
- **CK-FR-002**: A stored key MUST be encrypted with ASP.NET Core Data Protection (`IDataProtector`
  with the fixed purpose string `StemStudio.Agent.ClaudeApiKey.v1`) before it is written; the
  database MUST hold only the ciphertext, the last four characters and the time saved.
- **CK-FR-003**: The Data Protection key ring MUST be persisted in its own directory outside the
  library (`StemStudio:DataDir`), outside the backups folder, outside the application's content root
  and outside the repository: in Docker a separate named volume mounted only into `api`; on the
  non-Docker path a per-user directory. The API MUST refuse to store a key (not refuse to start)
  when the configured directory is inside one of those locations.
- **CK-FR-004**: The application name of the API's Data Protection MUST be set explicitly
  (`StemStudio.Api`) so the key ring and ciphertexts stay valid across container rebuilds, image
  updates and content-root changes.
- **CK-FR-005**: On Windows (non-Docker path) the key ring MUST be encrypted at rest with DPAPI for
  the current user; on Linux/macOS and in the container, where no at-rest key encryption is
  configured, the key ring directory MUST be readable and writable only by the app user (0700).

**API (write-only)**

- **CK-FR-006**: `PUT /api/agent/settings/claude-key` with `{apiKey}` MUST validate the format
  (after trimming: prefix `sk-ant-`, 40-256 characters, only `A-Z a-z 0-9 - _`, not an admin key)
  and store or replace the key; it MUST NOT contact Anthropic. `DELETE` on the same path MUST
  remove a stored key (idempotent). `GET` on the same path MUST return only the key status.
- **CK-FR-007**: `POST /api/agent/settings/test` with `backend: claude` MUST accept an optional
  `claudeKeySource` (`active` default, `stored`, `environment`) and remains the only call that
  contacts Anthropic outside a run (free model lookup).
- **CK-FR-008**: The key status MUST expose only `configured`, `source` (`environment` | `stored` |
  null), `state` (`not-configured` | `environment` | `stored` | `stored-unreadable`), `lastFour`,
  `stored` (`absent` | `readable` | `unreadable`) and `storedAt`, in `GET /api/agent/status`
  (`claude.key`) and in the key endpoints. `claude.keyConfigured` keeps its 011 meaning "a usable key
  is available" (now from either source).
- **CK-FR-009**: The key MUST NEVER appear in a response, problem detail, validation message,
  exception message, log line (any level), metric, trace, SSE event, run record, database row in
  plain text, backup README or manifest, or page. HTTP logging (if ever enabled) MUST log nothing of
  the key endpoint (`WithHttpLogging(HttpLoggingFields.None)` and a redaction interceptor); the
  request DTO MUST NOT print its value in `ToString()`.
- **CK-FR-010**: Bodies of the key endpoint larger than 1 KiB MUST be refused with 413; malformed
  JSON MUST answer a fixed 400 problem detail.
- **CK-FR-011**: A decryption failure MUST yield `state: stored-unreadable` with a readable reason
  and fix; it MUST NOT crash the API, block start-up or other endpoints, retry in a loop, or log the
  exception message or ciphertext.

**Local-only entry**

- **CK-FR-012**: `PUT`, `DELETE` of the key and the Claude key test MUST be accepted only from the
  computer itself: the API's endpoint filter accepts the request when the TCP peer is a loopback
  address or the configured internal caller (the `web` service on the compose network), the `Host`
  header is a loopback name or the API's compose service name, and a present `Origin` equals the web
  origin; otherwise `403 local-only`. The Web app MUST enable the key controls only on a circuit
  whose page was requested under a loopback host name (`localhost`, `127.0.0.1`, `[::1]`).
- **CK-FR-013**: Paired 018 remote devices MUST have no way to read, write, delete or test the key
  (no remote command; remote listener 404 for API paths as in 018 FR-013).

**UI**

- **CK-FR-014**: The Settings page MUST offer, in the Claude part of the agent section and on every
  user level: the key status, a password field (`autocomplete="off"`, never pre-filled, cleared
  after every save attempt), **Save key** (no stored key) or **Replace key** / **Delete key**
  (stored key, delete with confirmation), **Test key**, the local-only explanation and readable
  errors. The 011 text about `ANTHROPIC_API_KEY` stays as the alternative.
- **CK-FR-015**: Saving, replacing or deleting a key MUST NOT change the selected backend or the
  notice acknowledgement; selecting Claude still requires a usable key and the acknowledged notice
  (011 FR-018/019).
- **CK-FR-016**: A key change MUST apply to the next command; an active run keeps the client it
  started with, and the panel says so.

**Cross-feature**

- **CK-FR-017**: A 013 backup MUST NOT contain the key ring directory or any Data Protection key;
  the database snapshot MAY contain the ciphertext row. A 013 merge restore MUST NOT import the
  secret row; after a replace restore the restore report MUST say when the stored Claude key became
  unreadable.
- **CK-FR-018**: Help entry `agent-claude-key` (004 framework) MUST explain both sources, the
  precedence, what is protected and what is not (plan.md threat model, in plain words) and how to
  recover from `stored-unreadable`.

### Key Entities

- **Stored Claude key** (`AgentSecret` row, one per secret name; here `claude-api-key`):
  ciphertext, last four characters, saved at, purpose version.
- **Key status** (computed, never stored): configured, source, state, last four, stored state,
  saved at.
- **Data Protection key ring** (files in the key directory, not in the database).

## Success Criteria *(mandatory)*

- **CK-SC-001**: In automated tests with a sentinel key saved through the API, **0** occurrences
  of the sentinel (and of its base64 and UTF-16 forms) in: all API responses and problem details,
  captured logs at Trace level, SSE events, the SQLite file plus `-wal`/`-shm`, the rendered
  Settings DOM, and a 013 backup archive (when 013 exists).
- **CK-SC-002**: **0** requests to Anthropic during start-up, page loads, status polling, save,
  replace and delete ("must not be called" handler); exactly 1 per **Test key** press.
- **CK-SC-003**: A stored key survives `docker compose down` + `docker compose up --build` (key
  volume kept) in 100 % of runs on the reference machine (quickstart Q3).
- **CK-SC-004**: With the key volume removed (or a database from another installation), the API
  starts, status is `stored-unreadable` within one status call, and the full existing E2E suite
  stays green.
- **CK-SC-005**: Key endpoints from a non-loopback peer or host answer `403 local-only` in 100 % of
  the API tests; after 018, a LAN device cannot reach the API port at all (018 SC-004).
- **CK-SC-006**: Saving a key and seeing the new status takes <= 1 s (p95) on the reference
  machine; the Settings section passes the axe checks of 004 (WCAG 2.2 AA).

## Assumptions

- One local user, no accounts (011, 018). "Owner" means whoever sits at the computer.
- The Anthropic key format (`sk-ant-` prefix, base64url characters, about 108 characters today) is
  checked loosely (40-256 characters) so that a future format with the same prefix still fits.
- ASP.NET Core Data Protection is part of the shared framework `Microsoft.AspNetCore.App`; no new
  package or licence.
- 013 (library care) and 018 (tablet and remote) are designed but not built; their documents get
  cross-feature notes in this change. If 013 is built first, its tasks already contain the rules.
- Owner ratification of constitution 1.4.0 is required before any implementation task starts.
