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
