# Contract delta: UI test ids for the 011 follow-up "claude-key" (draft, additive)

Status: draft delta, **1.21.0** (ROADMAP allocation). Base: the full `ui-test-ids.md` current when
the follow-up is built (011's **1.10.0** or later). Merged by the architect into the full document
(task T002). Nothing below renames, removes or changes an existing id; every rule of 1.10.0 applies
(R1-R9: `getByTestId` only, level visibility, placement inside 004's layout). The 011 ids
`settings-agent-claude-status` (`data-key-configured`), `settings-agent-claude-notice*` and
`settings-agent-test*` keep their meaning; `data-key-configured` is now true for a usable key from
either source.

## Changelog line (for the merged document)

> 1.21.0 (additive, 011 follow-up claude-key) - Claude key part of the settings agent section:
> status with source and last four, key field, save, replace, cancel, delete with confirmation,
> test, local-only notice, environment-override note and error; agent panel "Switch to local"
> inside `agent-unavailable` and the key-changed note. All earlier ids are unchanged.

## Ids

All on the Settings page (`/settings`), inside the Claude part of the agent section, visible on
**all** levels (Beginner, Advanced, Expert).

| `data-testid` | Element / attributes | Present when |
|---|---|---|
| `settings-agent-claude-key` | the key group (`role="group"`, `aria-labelledby` its heading "Claude API key") | always |
| `settings-agent-claude-key-status` | status text; attributes `data-state` = `not-configured` / `environment` / `stored` / `stored-unreadable`, `data-source` = `environment` / `stored` / empty, `data-stored` = `absent` / `readable` / `unreadable`; live region `aria-live="polite"`; never the key (at most "ends with ...WXYZ") | always |
| `settings-agent-claude-key-env-override` | note "A saved key exists (ends with ...WXYZ, saved <date>) but is not used while ANTHROPIC_API_KEY is set" | `data-source="environment"` and `data-stored` != `absent` |
| `settings-agent-claude-key-unreadable` | explanation and fix of `stored-unreadable` ("Enter the key again") | `data-stored="unreadable"` |
| `settings-agent-claude-key-input` | `<input type="password" autocomplete="off" spellcheck="false">`, label "Claude API key", `maxlength="512"`; never pre-filled; emptied after every save attempt | no stored key, or after `settings-agent-claude-key-replace` was pressed |
| `settings-agent-claude-key-save` | button "Save key" (with a stored key: "Save new key"); disabled while the input is empty, while saving and when local-only applies | with `settings-agent-claude-key-input` |
| `settings-agent-claude-key-cancel` | button "Cancel"; hides the input again and empties it | after `settings-agent-claude-key-replace` |
| `settings-agent-claude-key-replace` | button "Replace key" | a stored key exists (`data-stored` != `absent`) and the input is hidden |
| `settings-agent-claude-key-delete` | button "Delete key"; opens the confirmation | a stored key exists |
| `settings-agent-claude-key-delete-confirm` | confirmation dialog (`role="alertdialog"`) with `settings-agent-claude-key-delete-confirm-yes` and `settings-agent-claude-key-delete-confirm-no` | after `settings-agent-claude-key-delete` |
| `settings-agent-claude-key-test` | button "Test key"; when both sources exist a choice `settings-agent-claude-key-test-source` (radio group: `-active`, `-stored`) | `data-state` is `environment` or `stored`, or `data-stored="readable"` |
| `settings-agent-claude-key-test-result` | result text; attribute `data-ok` = `true` / `false`, `data-key-source` = `environment` / `stored` | after a test |
| `settings-agent-claude-key-local-only` | notice "The Claude key can only be entered on the computer StemStudio runs on"; all key controls disabled | page opened under a non-loopback host name, or the API answered `403 local-only` |
| `settings-agent-claude-key-error` | readable error of the key endpoints (422 reason text, 503 key store, 413) - never the value | after a failed action |

### Agent panel

Agent panel (layout, all levels; added in 1.21.0 before its release, frontend us-p2 request). The
1.10.0 `agent-switch-to-local` stays the button inside `agent-run-error` only.

| Area | `data-testid` | Element / attributes | Present when |
|---|---|---|---|
| Agent panel | `agent-unavailable-switch-to-local` | button "Switch to local" inside `agent-unavailable`; selects the local backend (`PUT /api/agent/settings` `backend: ollama`) | the selected backend is Claude and it is not ready (no usable key, key deleted or `stored-unreadable`) |
| Agent panel | `agent-key-changed` | note (`role="status"`) "Key changed - applies to the next command. The active run keeps the key it started with; cancel it to use the new key now." (CK-FR-016) | a key was saved, replaced or deleted while a run is active; removed when that run ends |

## Help topic

`agent-claude-key` (004 help framework) on `settings-agent-claude-key`: the two sources and their
precedence, "stored encrypted on this computer", what this protects and what it does not, how to
replace a leaked key, and the recovery from `stored-unreadable`.

## Rules for qa

- Tests never type a literal key-shaped string: the sentinel is built at run time (repository
  secret scan, research R-07).
- After a save, assert that the DOM (`page.content()`) and the input value do not contain the
  sentinel.
