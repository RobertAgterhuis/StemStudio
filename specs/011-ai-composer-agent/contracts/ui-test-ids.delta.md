# Contract delta: UI test ids for feature 011 (draft, additive)

Status: **merged** into [ui-test-ids.md](ui-test-ids.md) **1.10.0** (011 Phase 0 part 2, 2026-11-16;
009/010 added no settings page, so `/settings` is new; three ids added at the merge:
`agent-approval-effect`, `agent-approval-estimate`, `nav-settings`). The full document is binding;
this file is kept for the review history. Original status: draft delta (011 Phase 0 part 1, 2026-11-15). Base: `specs/008-sheet-music/contracts/ui-test-ids.md`
**1.7.0**. Features 009 and 010 take the next two minors (expected 1.8.0, 1.9.0); in Phase 0 part 2
the architect merges this delta unchanged into the full `specs/011-ai-composer-agent/contracts/ui-test-ids.md`
as the following minor (expected **1.10.0**). Nothing below renames, removes or changes an existing
id; every 1.7.0 rule applies (R1-R9: `getByTestId` only, level visibility, placement inside 004's
layout).

Binding for the **frontend** agent once merged; the **qa** agent may already author the agent specs
against these names (T040, T049, T058, T067, T074).

## Changelog line (for the merged document)

> 1.10.0 (additive, feature 011) - agent panel on every page (toggle, conversation list, messages,
> plan, steps with progress, approval cards, backend badge, unavailable state, undo, cancel, usage,
> tool log) and the agent section of the settings page. All 1.9.0 ids are unchanged.

## Ids

| Screen | `data-testid` | Element / attributes | Levels |
|---|---|---|---|
| Layout (every page) | `agent-toggle` | button that opens/closes the panel (`aria-expanded`) | all |
| Layout | `agent-panel` | the panel (complementary landmark, `aria-label="Composer agent"`) | all |
| Layout | `agent-backend-badge` | attributes `data-backend` = `ollama`/`claude`/`none`/`fake`, `data-model` (model id or empty), `data-fallback` = `true`/`false` | all |
| Layout | `agent-unavailable` | exists only while the backend is not ready; contains the reason and the fix text of `GET /api/agent/status` | all |
| Agent panel | `agent-input` | multi-line text input (max 4,000 characters); Enter sends, Shift+Enter new line | all |
| Agent panel | `agent-send` | send button; disabled while not ready, while a run is active or when the input is empty | all |
| Agent panel | `agent-cancel` | exists only while a run is active | all |
| Agent panel | `agent-undo` | enabled when something can be undone | all |
| Agent panel | `agent-new-conversation` | starts a new conversation | all |
| Agent panel | `agent-conversation-list` | list of conversations, newest first | all |
| Agent panel | `agent-conversation-<conversationId>` | one list entry (button); `aria-current="true"` on the open one | all |
| Agent panel | `agent-conversation-rename-<conversationId>`, `agent-conversation-delete-<conversationId>` | entry actions (delete disabled while its run is active) | Advanced, Expert |
| Agent panel | `agent-message-<seq>` | one message; attribute `data-role` = `user`/`assistant` | all |
| Agent panel | `agent-streaming` | exists only while answer text streams (live region `aria-live="polite"`) | all |
| Agent panel | `agent-plan` | plan checklist (exists only when the run has a plan) | all |
| Agent panel | `agent-plan-item-<index>` | attribute `data-status` = `pending`/`running`/`done`/`failed`/`skipped` | all |
| Agent panel | `agent-step-<seq>` | step row; attributes `data-tool`, `data-status` (AgentStepStatus wire value), `data-risk` | Advanced, Expert (Beginner sees the plan only, spec Clarifications) |
| Agent panel | `agent-step-progress-<seq>` | progress bar of a job step, `aria-valuenow` 0-100 | all |
| Agent panel | `agent-step-error-<seq>` | readable error of a failed step | all |
| Agent panel | `agent-approval-card` | confirmation card; attributes `data-tool`, `data-risk`, `data-reasons` (comma-separated reason codes, e.g. `risk-heavy,tainted`) | all |
| Agent panel | `agent-approval-arguments` | the exact arguments shown on the card | all |
| Agent panel | `agent-approve`, `agent-reject` | card buttons; Approve never receives focus automatically | all |
| Agent panel | `agent-approve-plan` | exists only when `allowPlanScope` is true | all |
| Agent panel | `agent-run-error` | readable failure with the fix; contains `agent-switch-to-local` when the failed backend is Claude | all |
| Agent panel | `agent-switch-to-local` | button inside `agent-run-error` (Claude failures only) | all |
| Agent panel | `agent-usage` | token usage of the run (Claude only) | Advanced, Expert |
| Agent panel | `agent-tool-log` | raw step log (tool, arguments, result summary) | Expert |
| Settings (`/settings`, new page: 004 has no settings route) | `settings-agent-backend` | radio group with `settings-agent-backend-ollama`, `settings-agent-backend-claude`, `settings-agent-backend-none` | all |
| Settings | `settings-agent-ollama-status` | Ollama reachability, version, models | all |
| Settings | `settings-agent-ollama-model`, `settings-agent-ollama-fallback-model` | model pickers (only tool-capable models) | Advanced, Expert |
| Settings | `settings-agent-claude-status` | attribute `data-key-configured` = `true`/`false`; never the key | all |
| Settings | `settings-agent-claude-model`, `settings-agent-claude-effort` | Claude pickers | Expert |
| Settings | `settings-agent-claude-notice`, `settings-agent-claude-notice-ack` | paid-service notice (what is sent, paid per use) and its acknowledge checkbox | all |
| Settings | `settings-agent-test`, `settings-agent-test-result` | connection test button and its result | all |
| Settings | `settings-agent-save`, `settings-agent-error` | save button and readable error (409/422 of the settings endpoint) | all |

## Open points for the merge (Phase 0 part 2)

- Confirm the minor number after 009/010 and whether 009/010 add a settings page first (then the
  agent section is placed inside it and only the `settings-agent-*` ids are added).
- Add the agent topics to the help catalogue (`help-content` contract 1.0.0 rule: every feature
  adds help): `agent-panel`, `agent-approvals`, `agent-backends` (frontend, wave).
