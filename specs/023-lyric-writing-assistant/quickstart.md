# Quickstart: Lyric Writing Assistant (feature 023)

Validation walk-through for the integration wave (CTO) and qa. Q1-Q6 run on the fake stack (CI
equivalent); Q7-Q10 on the reference machine with Ollama (and optionally Claude, owner).

## Prerequisites

- 010 and 011 delivered, 011 claude-key merged; 023 branch built (`dotnet build` 0 warnings).
- Fake stack: `STEMSTUDIO_AGENT_BACKEND=fake`, `STEMSTUDIO_AGENT_FAKE_SCRIPT=/scripts/lyric-agent.json`,
  `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT=/scripts/lyric/zomeravond-check.json`, fake lyrics fixture
  (`STEMSTUDIO_FAKE_LYRICS`) seeded for track `t-fake-song`.
- Reference machine: Ollama 0.40+ with `qwen3.5:9b` and `qwen3.5:4b` pulled (011 F-16).

## Q1 - Check a Dutch sheet (US1, fake model)

1. Create the sheet "Zomeravond" (language Dutch) from `tests/fixtures/lyrics/assist/sheets/zomeravond.nl.txt` (Phase 0 fixture; facts in `assist/expected.json`).
2. Press **Check** (`lyric-assist-check`). Expect dictionary suggestions at once, then streamed
   model suggestions; `lyric-assist-layers` names OpenTaal and the fake model.
3. Expect the planted corrections (2 spelling, 1 grammar, 1 flow) and **no** suggestion on `'k`,
   `'t`, `Gij zijt`, `Naar huis ik ga`.
4. Accept one spelling fix: only that span changes, revision +1, `lyric-line-provenance-<n>` shows.
   Press **Undo**: the text is back, the suggestion is open again.
5. **Accept all** with filter spelling: one revision; Undo restores both.

## Q2 - Poetic licence controls (US1/US2)

1. Set sensitivity **strict**: the licences appear as `idea` notes ("poetic licence (recognised)").
2. On a flagged word choose **Mark as intentional**; check again: not flagged. Remove the mark in
   `lyric-marks-list`; check again: flagged again.
3. **Add to my words** on an unknown name; check another Dutch sheet with that name: not flagged.

## Q3 - Devices, writing help, syllables, rhymes (US2/US3)

1. Set the rhythm target 92 BPM, 4 beats, 2 bars, 1.5 syllables per beat; switch on the syllable
   gutter: line counts appear, line 2 of the fixture (15 syllables against the target 12) is marked as
   deviating (Phase 0: line 1 is the `[verse]` marker).
2. **Suggest devices**: a syllable-fit suggestion on line 2 with "15 -> 12 syllables" and an end-rhyme
   suggestion naming the rhyme word; both verified.
3. **Alternatives** on line 4 (count 3): three options; accepting one supersedes the others.
4. **Continue** after the first verse: an insertion suggestion; nothing inserted before accept.
5. Rhyme finder for `nacht` (nl) and `night` (en): results within 500 ms, no model call (the fake
   model counter stays unchanged).
6. Instruction "zoals Marco Borsato": refused with the imitation message.

## Q4 - Agent (US4, scripted agent backend)

1. In the editor, agent panel: "Controleer deze tekst op spelling" -> step `check_lyrics`, answer
   with counts and a link to the pane; no lyric text in the step log (Expert).
2. "Accept all the spelling suggestions" -> approval card with `agent-approval-lyrics-diff` rows
   (before -> after); reject -> nothing changes; repeat and approve -> revision +1.
3. Undo in the agent panel undoes the apply (inverse).

## Q5 - Guardrails (SC-005)

1. Create a sheet with the text of the fake lyrics of `t-fake-song`: every assistant button shows
   `lyric-assist-not-own` naming the track.
2. Load `e2e/lyric-scripts/alternatives.json`: `lyric-assist-withheld` shows 1; the
   withheld text is nowhere in the page.
3. Sheet `l-inject` with an injection line: suggestions have valid anchors only; nothing else changes.
4. Own-work override (US6): on the refused sheet of step 1 press `lyric-assist-own-work-open`; the
   dialog shows the statement and the matched track; confirm stays disabled until name and tick box
   are set; confirm -> `lyric-own-work-badge` shows name, date and track; Check runs.
5. With the override, load `alternatives.json` again: the 9-word run is still withheld.
6. With the override, instruction "like Ed Sheeran": still refused (`imitation`).
7. `lyric-own-work-revoke`: the badge disappears; Check is refused again.

## Q6 - Local-only and degraded modes (US5, SC-006)

1. Backend `none`: Check runs the dictionary layer; `lyric-assist-unavailable` lists grammar, flow
   and writing help with the 011 fix; Alternatives/Continue/Devices are disabled with the reason.
2. The outbound-request guard (test handler) records 0 requests during Q1-Q6.
3. Backend `fake-claude` (test double of the Claude backend): the first model action shows
   `lyric-assist-claude-notice`; cancel sends nothing; acknowledge -> the assist runs and usage is
   shown (Advanced).

## Q7 - Reference machine: latency (SC-004)

`dotnet run --project tools/agent-eval -- --suite lyric --cases P1,P2 --runs 3` with Ollama 9b warm:
dictionary < 1 s, first model suggestion <= 6 s p50, total <= 30 s p50 / 45 s p95. Start a detail
separation and repeat P1: the fallback model runs on the CPU (badge), total <= 180 s.

## Q8 - Reference machine: quality (SC-001, SC-002)

`dotnet run --project tools/agent-eval -- --suite lyric --runs 3`: report recall/precision/licence
FP per language and category; compare with SC-001. Owner rates 10 device and 10 writing
suggestions per language (median >= 3).

## Q9 - Accessibility (SC-008)

Keyboard only: Ctrl+Alt+C, F8/Shift+F8, Alt+Enter, Alt+Backspace, Alt+I, Escape; NVDA announces the
finished check and each focused suggestion; axe clean on the editor with the pane open.

## Q10 - Optional Claude (owner)

With a Claude key configured (011 claude-key), select Claude, acknowledge both notices, run the
lyric suite once; record the Claude numbers next to the local ones.
