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

## frontend

- WCAG 2.2 AA in bUnit tests: accessible name contains the visible text (2.5.3), target size, and
  focus never covered by the fixed bottom bars; use the dock-inset pattern
  (`wwwroot/js/dock-inset.js`, `scroll-padding-bottom`). (012: two axe target-size failures)
- `RadzenButton` ignores clicks while its previous click handler is still running: do not await the
  audio engine inside rapid-click handlers. (012: lost +1 / +5 % clicks)
- A new navigation item: check the rail at 720 px height and at 320 px width (013: Help and
  Settings were cut off).

## backend

- Runtime SQLite connections use `Mode=ReadWrite`; only start-up may create the database
  (`LiveDatabaseFile`). A connection with the default `ReadWriteCreate` during a file switch creates
  an empty live database. (013: restore/undo data-integrity bug)
- A database or archive from outside is untrusted: rebuild from our own migrations and copy only
  row data, instead of validating its DDL. Validators kept leaking. (013: three security rounds)
- Every stored or archived path is checked for containment in its own root before use. (013 F1)

## qa

- Wait on the job id (`data-job-id`) of the job you started, not only on a status: the panel may
  still show the previous finished job. (013: backup E2E race)
- Lanes do not start E2E stacks; write specs against contracts and test ids, typecheck and lint
  them. The integration runs E2E.
- The default `npm test` includes the feature specs: plan about 1.2 h for it at integration. (013)

## devops

- Every stack in CI or E2E gets its own temporary folders with the right owner (uid 1654 on
  Linux); Windows bind mounts show 777, so ownership checks apply to Linux only. (013)

## Integration (CTO)

- Rebuild generated docs (manual PDFs, manifest) as the very last step and run the CI checks
  locally afterwards; an edit to STATUS after the PDF build turned `main` red. (012)
- Run heavy suites one after the other; `test-all` in parallel with E2E gave timing flakes. (013)
- Make visual baselines only in the environment of the CI visual job. (013)
