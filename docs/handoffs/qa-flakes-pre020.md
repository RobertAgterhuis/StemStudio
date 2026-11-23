# Hand-off: qa / flakes-pre020

## What was built

The known E2E flakes before round 020, each with a root cause, fixed in the specs (no product code changed) or kept
quarantined, and a new flake list `docs/process/FLAKES.md` (test, symptom, cause, status).

1. `agent.spec.ts` `@us4-lyric-apply`: test race. After Reject the fake run goes on to the script's final turn; only
   one agent run may be active, so the next command was refused with `409 run-active`. The test now waits for the
   rejected run's answer and the `rejected` step.
2. `export.spec.ts` SC-004: test race on a product edge case. An item saved while the track's band-info lookup is still
   `searching` is filed under "Unknown artist > Unknown album" and re-filed when the lookup ends (proven through the
   API), so `export-saved-open` opened a song folder without the row. New `e2e/support/settled-track.ts`
   (`ensureSettledTrack`) is used by `export.spec.ts`, `sheet-music.spec.ts` and the drop D test of
   `instrument-tab.spec.ts` (all three follow a save link).
3. `practice-log.spec.ts` scenario 2: test bug. The spec pressed `transport-play` to pause; that button only plays.
   New `MixerPage.pause()` (`transport-pause`); scenario 2 pauses 1 s into pass 3 and reads the pause write before
   leaving; scenario 1 waits for the write after its second pause (at least 5 s).
4. `instrument-singing.spec.ts:22` fake-mic on-pitch: stays `test.fixme`. The reference melody comes from the worker's
   fake transcriber (0.5 s notes); fixing it needs the worker fake and the qa fixture changed together (R-11).

## Files changed

- `e2e/support/settled-track.ts` (new), `e2e/pages/mixer-page.ts`
- `e2e/tests/agent.spec.ts`, `export.spec.ts`, `sheet-music.spec.ts`, `instrument-tab.spec.ts`, `practice-log.spec.ts`
- `docs/process/FLAKES.md` (new; outside the qa paths, created on the orchestrator's instruction)

## Tests and coverage

Isolated stack: compose project `e2eflk`, own images `e2eflk/*`, ports 127.0.0.1:28080/28081, fake separator,
`retries=0`; torn down with `down -v` afterwards. Logs: `.claude/scratchpad/flakes-pre020/`.

| Flake | Before the fix | After the fix |
|---|---|---|
| agent `@us4-lyric-apply` | 1 failed / 10 (Chromium), same symptom as reported | 50 / 50 passed (Chromium + Firefox, `--repeat-each` 10 and 15) |
| export SC-004 | 10 / 10 passed in isolation (race needs a lookup still in flight) | 20 / 20 (Chromium + Firefox x10), 3 / 3 on a freshly imported track, whole `export.spec.ts` 11 / 11 |
| sheet-music link, instrument-tab drop D | - | 5 / 5, 3 / 3 |
| practice-log scenario 2 | 15 / 15 passed in isolation; the no-op pause was observed directly | whole file x10 Chromium 40 / 40, x3 Firefox 12 / 12 |

E2E specs only: no coverage gate applies. `scripts/test-all.sh --affected`: see the summary in the final report of this
task.

## Quality checks

`npm run lint` (eslint, max-warnings 0): clean. `npm run typecheck` (tsc --noEmit): clean.

## Dependencies added

None.

## Docs impact

None (test-only change).

## Open issues / requests

- Product (frontend/backend): the save confirmation link (`export-saved-open`, `score-saved-open`) names the song
  folder at save time and goes stale when a running band-info lookup re-files the item seconds later. A user who
  exports right after an import lands in an empty or missing folder. Option: link to `library?asset=<id>` and resolve
  the folder when the link is opened.
- Audio engine (for information): the practice-log close write after a full page load depends on the old circuit
  being disposed; under load it can land after 15 s. The recorder documents "at most 30 s lost on a crash", so not a
  defect, but specs must read a pause write, not the leave write.
- Worker + qa: longer reference notes in the fake transcriber and `generate_instrument_fixtures.py` (R-11) to re-enable
  the fake-mic on-pitch test.
- `library-care-backup.spec.ts:159` failed in both browsers in one 016 integration run and passed in the next; listed
  as open in `FLAKES.md`, not analysed in this task.

## Next owner

Orchestrator (integration of round 020).
