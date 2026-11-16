# Hand-off: frontend / 011 follow-up claude-key, checkpoint polish (T049) and lane summary

Branch `ck-lane-frontend` (from `011-claude-key-spec`). Nothing pushed or merged; no docker; Anthropic never called; no
key-shaped literal (sentinels from `tests/Shared/ClaudeKey/ClaudeKeySentinels.cs`, linked into Web.Tests). Earlier
checkpoints: `frontend-ck-us-p1.md`, `frontend-ck-us-p2.md`.

## What was built

- **Help topic `agent-claude-key`** (`Help/topics/start/agent-claude-key.md`, no route, all levels): the two sources, "the
  environment variable wins", what the page shows (source, last four characters, save time), that saving does not select
  Claude, local-only entry (`http://localhost:8080`), Test key (free, only on click), what the encryption protects (database
  copy, backups, exports, logs, screenshots) and what it does not (full access to the computer, malware as the owner, the
  running program) in plain words (plan.md T1-T4, T8, T9), replace after a leak (revoke in the console first), delete, and the
  recovery from "cannot be read" ("Enter the key again"). Anchor `data-help-id="agent-claude-key"` on `settings-agent-claude-key`.
- **`agent-backends`** topic: the sentence "StemStudio never shows or stores the key" replaced (the key can now be stored
  encrypted; link to `help:agent-claude-key`).
- **Accessibility checks** (bUnit, `ClaudeKeyHelpAndAccessibilityTests`): every input and button of the section has an
  accessible name (label, `aria-label` or text), the test-source radio group is labelled, the status is `aria-live=polite`,
  the group is labelled by its heading, the confirmation takes the focus on "Keep key" and Escape keeps the key. The real
  axe scan (WCAG 2.2 AA) of the rendered Settings page is qa's Playwright suite (request 1).

## Files changed

- New: `src/StemStudio.Web/Help/topics/start/agent-claude-key.md`, `tests/StemStudio.Web.Tests/Settings/ClaudeKeyHelpAndAccessibilityTests.cs`; this note.
- Modified: `src/StemStudio.Web/Help/topics/start/agent-backends.md`, `src/StemStudio.Web/Components/Agent/ClaudeKeySection.razor`
  (help anchor); `specs/011-ai-composer-agent/followups/claude-key/tasks.md` (T034, T035, T040, T041, T044, T045, T049 ticked).

## Tests and coverage

`dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` -> **Passed 1839, Failed 0, Skipped 0**;
StemStudio.Web **line 99.57 %, branch 93.79 %, method 99.68 %** (gate 90/90 passed). Only Web.Tests was run.

New claude-key code (Cobertura, lines / branches): `ClaudeKeyText` 52/52, 64/64; `ClaudeKeyErrors` 46/46, 30/32;
`PageOrigin` 10/10, 8/8; `AgentPanelState.Key` 10/10, 18/20; `ClaudeKeySection` razor 30/30, 44/44 and code-behind
80/80, 28/32; `ClaudeKeyTest` razor 8/8, 24/24 and code-behind 14/14; `ClaudeKeyDeleteDialog` 8/8, 8/8. Together **258/258
lines (100 %), 224/232 branches (96.6 %)**.

## Quality checks

- `dotnet build src/StemStudio.Web -c Release` -> 0 warnings, 0 errors.
- `dotnet format src/StemStudio.Web/StemStudio.Web.csproj --verify-no-changes` and the same for Web.Tests -> exit 0.
- `uv run --project tools/quality python tools/quality/run_all.py` -> **0 errors**, 760 warnings, exit 0. Warnings in this
  lane's files are test-only: two tests of 16 LOC (`ClaudeKeySectionTests.Save_StoresTheKey...`,
  `ClaudeKeyReplaceDeleteTests.AKeyChange_DuringAnActiveRun...`) and the shared `using` block counted as 7-line clones.
  Production code of the lane has no finding.
- `python scripts/secret_scan.py` -> 3509 files clean.

## Dependencies added

None.

## Docs impact

User manual, Settings -> Composer agent -> **Claude API key** (CTO, T055): entering the key (Save key, the field empties, only
the last four characters are shown), precedence of `ANTHROPIC_API_KEY`, local-only entry, Replace / Delete (confirmation, no
backend switch), Test key, the recovery from "cannot be read on this installation", and the panel note "Key changed - applies
to the next command". The in-app help topic `agent-claude-key` can serve as the source text.

## Open issues / requests

1. **qa**: include the Settings page with the key section (states not-configured, stored, environment+stored, unreadable,
   delete confirmation open) in the Playwright axe scan (CK-SC-006); open it as `localhost`.
2. **architect**: ui-test-ids for `agent-unavailable-switch-to-local` (and optionally `agent-key-changed`), see us-p2 request 1.
3. **backend**: send `reason` on every 422 `claude-key-invalid`, `type .../local-only` on the 403, fill `claude.key` in
   `GET /api/agent/status`, `ready: false` with a reason for Claude without a usable key, and `keySource` in Claude test results.
4. **CTO**: hand-off names follow the coordinator (`frontend-ck-<checkpoint>.md`); tasks.md names
   `<owner>-011-claude-key-<checkpoint>.md`.

## Next owner

CTO (integration wave T053-T056) and qa (T046-T048).
