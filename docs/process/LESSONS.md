# Lessons learned (feature rounds)

One place for lessons from past rounds. Role files in `.claude/agents/` point here; read the
section for your role before you start. The orchestrator appends 3-5 lines after every round.

## All roles

- Never touch the owner's running stack (compose project `stemstudio`, its `stemstudio/*` images
  and volumes). E2E and spikes run in an own compose project with own ports, images and volumes,
  torn down with `down -v` afterwards. (011-013)
- Do not cut scope or deviate from a contract on your own: message the orchestrator and wait for
  the decision. (013: backend cut merge-restore while frontend and qa had built it)
- Run tests quietly, write full logs to a file and read only summaries and failures. (token cost)
- Run `dotnet format StemStudio.slnx --verify-no-changes` on the whole solution before a hand-off, not
  per project: 197 whitespace findings of one lane reached the integration. (022/023)
- An agent that waits on a background run polls its log for progress and stops a run that has not moved for
  10 minutes; heavy suites run in the foreground with an explicit timeout where possible. (022/023: an agent
  waited 2.5 h on a hung run)

## frontend

- WCAG 2.2 AA in bUnit tests: accessible name contains the visible text (2.5.3), target size, and
  focus never covered by the fixed bottom bars; use the dock-inset pattern
  (`wwwroot/js/dock-inset.js`, `scroll-padding-bottom`). (012: two axe target-size failures)
- `RadzenButton` ignores clicks while its previous click handler is still running: do not await the
  audio engine inside rapid-click handlers. (012: lost +1 / +5 % clicks)
- A new navigation item: check the rail at 720 px height and at 320 px width (013: Help and
  Settings were cut off).
- Check every contract role against ARIA at design time: a `listbox` whose options hold buttons violates
  `nested-interactive` (an option may not contain interactive content); use a plain list with focusable entries.
  (023: 12 of 16 axe failures on the suggestion list)
- Never combine `value="@x"` with a separate `@oninput` on a text field: every render writes the
  server value over keys still in flight. Use `@bind:get`/`@bind:set` with `@bind:event="oninput"`.
  (022: the library search lost typed characters; about ten other fields still use the pattern)

## backend

- Runtime SQLite connections use `Mode=ReadWrite`; only start-up may create the database
  (`LiveDatabaseFile`). A connection with the default `ReadWriteCreate` during a file switch creates
  an empty live database. (013: restore/undo data-integrity bug)
- A database or archive from outside is untrusted: rebuild from our own migrations and copy only
  row data, instead of validating its DDL. Validators kept leaking. (013: three security rounds)
- Every stored or archived path is checked for containment in its own root before use. (013 F1)
- The live SQLite database must be in WAL mode: `LiveDatabaseFile.UseWal` at every start and in the
  library switcher before the gate opens after a restore/undo. (022/023 flakes2: `database is locked`)
- "Only one X at a time" needs one lock around check, insert and start; a database check alone lets
  two concurrent requests both start (023 review: two assists, the first one uncancellable).

## qa

- Wait on the job id (`data-job-id`) of the job you started, not only on a status: the panel may
  still show the previous finished job. (013: backup E2E race)
- Lanes do not start E2E stacks; write specs against contracts and test ids, typecheck and lint
  them. The integration runs E2E.
- The default `npm test` includes the feature specs: plan about 1.2 h for it at integration. (013)
- A wait on a container attribute that is already set while the view loads (`data-level="root"` on the
  skeleton) is no wait: wait on an element that exists only once the data is in. (022: library-browse root tiles)

## devops

- Every stack in CI or E2E gets its own temporary folders with the right owner (uid 1654 on
  Linux); Windows bind mounts show 777, so ownership checks apply to Linux only. (013)
- A new step in `test-all.*` goes below the right `group` line, and a new top-level folder or
  input needs a rule in `scripts/affected/components.json`; an unmapped file makes every
  `--affected` run full, and the map test fails on a tracked file that no rule covers. Python on
  Windows prints CRLF: strip `\r` before a shell parses its output. (proposal 001)

## Integration (CTO)

- Rebuild generated docs (manual PDFs, manifest) as the very last step and run the CI checks
  locally afterwards; an edit to STATUS after the PDF build turned `main` red. (012)
- Run heavy suites one after the other; `test-all` in parallel with E2E gave timing flakes. (013)
- Make visual baselines only in the environment of the CI visual job. (013)
- A lane must not report SIG green when jscpd errored: `run_all.py` now fails when jscpd cannot run;
  check the duplication step's own output at every hand-off. (022/023: 9 clones found only at integration)
- Several lanes ticking one `tasks.md` gives a conflict on every merge; resolve by keeping every tick
  (union per row), or let lanes tick only in their hand-off. (022/023)
- jscpd can fail silently (no report, exit 0 through a wrapper): the integration reads the duplication step's own
  output, not only the summary line of `run_all.py`. (022/023)
- WAL mode must be set at every start and again after every library switch (restore/undo), or a later
  `database is locked` shows up only as an E2E flake. (013 flakes2, 022/023)
- The specs of a lane must pass on a real stack at the mid-wave checkpoint, not only typecheck: the 022/023 E2E
  failures surfaced only at integration, when every fix costs a full default-suite rerun. (022/023)
- Watch `main` CI to the very end after every merge, every job: 013's E2E job was red (backups folder EACCES)
  and nobody noticed through the whole 022+023 round. (014/015)
- The single CI E2E job reached its 90-minute limit with 014/015 (raised to 150 min as a stopgap); split the
  default suite from the feature passes (recording, practice-flow, agent, lyric, restart, gpu) into parallel jobs
  before the next round adds specs. (014/015)
- Hook requests through the orchestrator worked, but a decision must reach every lane it affects: L5 built
  extra `shortcut-record`/`shortcut-compare` ids after the orchestrator had chosen the generic ids for L6. (014/015)
- Decisions a lane asks to be confirmed ("please confirm or redirect") need an explicit answer in the lane's
  next brief; unanswered ones (and contract wording such as `stopRecording` "stops") surfaced only at
  integration. (014/015)
- A per-agent token cap (150k for the Tipoca trooper lanes) stopped lanes mid-task with uncommitted WIP; keep a
  green committed state and a resume note (`handoff-resume.md`) at every checkpoint so a fresh agent can continue. (016)
- A red test must compile: an uncompilable red commit (L5 T105, T127) turns the whole solution build red for every
  other lane and the checkpoint; add the stubs the test needs in the red commit. (016)
- Fakes must agree with the research: the fake drum split had no hi-hat stem although research R-11 lists it, so specs
  and a lane reading were built on the fake's gap and the visual baselines changed at integration. Check every fake
  against the research and contract examples when it is written. (016)
- `toBeVisible()` never passes on an element with zero width by design (banner chords overflow above their word);
  assert presence and position (count, bounding box of the anchor word) instead, and check such assertions on a real
  stack at the checkpoint. (016)
