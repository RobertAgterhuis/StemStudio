# 016 Unify job persistence, restart reconciliation and delete semantics across the worker lanes
- Status: proposed · Date: 2026-11-19 · Area: worker/jobs (+ generator/jobs/store) · Priority: P2

## Problem statement
The worker has grown one job lane per feature (separation, detail, metadata 005, lyrics 006, render 007,
transcription 008, practice 014) and each brought its own variant of the same persistence mechanics.
`jobs/` is now 43 modules / 4,916 lines (`wc -l worker/src/stemstudio_worker/jobs/*.py`). Evidence:

**Four store families for one concept**
- `JsonStore` → `JobStore` (`jobs/store.py`), `DetailJobStore` (`jobs/detail_store.py`)
- `JobRecordStore` → `LyricsJobStore` (`jobs/lyrics_store.py`), `MetadataJobStore` (`jobs/metadata_store.py`)
- `LaneJobStore` → `RenderJobStore`, `TranscriptionJobStore`, 3 × `PracticeJobStore`
- generator: an unrelated `JsonJobStore` (`generator/src/stemstudio_generator/jobs/store.py`).

**Three restart code paths** (the per-lane *policy* is contract-documented — e.g.
`specs/014-practice-flow/contracts/worker-api.openapi.yaml:833-834` "failed" vs `:954` lyrics "run again" — but the
*mechanism* is re-implemented): `jobs/reconcile.py:28-47` and `:50-63` (separation, detail), `jobs/lane_store.py:33-39`
(render, transcription, practice), `jobs/lyrics_lane.py:107-110` and `jobs/metadata_lane.py:135-140` (re-queue).
The interrupted text is defined three times: `jobs/progress.py:32`, `jobs/detail_texts.py:18`, `schemas.py:515`.

**Three delete semantics**
- `LaneJobStore.remove` validates the id with `fullmatch` before `rmtree` (`jobs/lane_store.py:41-45`);
- `LyricsJobStore.remove` removes the folder only when the record was known (`jobs/lyrics_store.py:61-66`);
- `MetadataJobStore.remove` calls `rmtree(paths.metadata_job_dir(job_id))` with no check at all
  (`jobs/metadata_store.py:73-76`) — safe today only because the sole caller is the sweep with known ids
  (`jobs/metadata_lane.py:172-177`);
- generator `JsonJobStore.delete`/`get` build paths from the raw id (`jobs/store.py:47-61`) and rely on the
  route regex (which uses `match` with `$`, see the ffmpeg-hardening proposal).

**Atomic write copied four times, none durable**: tmp + `replace` in `jobs/json_store.py:41-48`,
`jobs/lyrics_store.py:47-53`, `jobs/metadata_store.py:45-53`, generator `jobs/store.py:29-35`; the score files
are written non-atomically (`jobs/transcription_store.py:43-54`). No `fsync` anywhere
(`grep -rn fsync worker/src generator/src` is empty). After a power loss a truncated `<id>.json` is "skipped as
unreadable" on load (`jobs/json_store.py:70-75`), so the job vanishes instead of being reconciled as
"interrupted by restart" — the restart guarantee of ADR-003 does not hold for that case.

## Impact
- Job-state correctness: the restart and delete guarantees differ per lane in ways the contracts do not
  describe (silent loss on a torn write; unvalidated `rmtree` waiting for a second caller).
- Each new lane copies ~200–300 lines (store + service + lane + reconcile) and its own tests; Sigrid
  duplication and component-independence suffer; reviewers must re-check the same invariants every round.

## Risk
- Leaving it: medium — the next lane will copy whichever variant is nearest; a new caller of
  `MetadataJobStore.remove` with a request id would be a path-traversal bug.
- Change: medium. Persisted JSON shapes must stay readable (existing data directories); the restart policy
  per lane must stay exactly as contracted. Rollback per lane (each migration is one PR).

## Effort
M–L (5–8 days): one generic lane store with a `RestartPolicy` (fail | requeue) and a single
`atomic_write(path, bytes)` helper, then migrate lane by lane.

## Step-by-step plan
1. Safety net: one parametrised characterisation test per lane that (a) writes a queued and a running record,
   restarts the service and asserts the contracted outcome, (b) deletes an unknown, an invalid (`..`,
   `a/b`, trailing `\n`) and a known id, (c) loads a truncated JSON file and asserts the current behaviour.
2. Add `jobs/atomic.py` (`write_text_atomic` / `write_bytes_atomic` with `fsync` of file and directory) and use
   it in the four copies and in `TranscriptionJobStore.save_files`.
3. Decide (ADR-003 amendment) what a truncated record means: keep the file aside as `<id>.json.corrupt` and
   log, so the backend sees a 404 rather than a silently missing job — or reconstruct a failed record when the
   id is known from the folder. Implement in `JsonStore._read`.
4. Make `JsonStore.delete`/`remove` validate the id once (`fullmatch`) for every store; delete the per-store
   checks; give `MetadataJobStore` and `LyricsJobStore` the `LaneJobStore` delete/sweep.
5. Introduce `RestartPolicy` on the lane store (`FAIL` for separation/detail/render/transcription/practice,
   `REQUEUE` for lyrics/metadata) and route all reconcile paths through it; one `INTERRUPTED` constant in
   `schemas.py`.
6. Optional: apply the same helper and id check to the generator store.
7. Verify: the step-1 tests unchanged and green, both suites green, coverage ≥ 90 %; restart the compose
   stack during a job of each lane (quickstart V6). Rollback: per-lane PR revert.

## Dependencies and related proposals
- After 020-split-worker-hub-modules (lands in the new layout); related to
  005-worker-ffmpeg-input-hardening (generator id regex).
