# qa 020 wave hand-off (T013, T028, T029, T048, T055, T080, T087, T101; T102 at integration)

Lane QA-OPS, branch `020-lane-qa-ops`. Commits: `1bc803ac` (T028 red), `352dcde5` (T029), `c796d85d` (T048,
T055), `12a51e38` (T080, T087, T101 + the `insights` E2E pass), `b3f5fd5e` (T013), plus the feature-skip fix of the
seeding hooks. G2's T099 is not built (deferred).

## Delivered

| Item | Where |
|---|---|
| Test strategy section 23: levels, V1 synthetic validation, fake analyzer, per-family fixtures, PDF inspection, specs, readings, FR/SC traceability (every FR and SC except FR-007/SC-007) | `docs/testing/TEST-STRATEGY.md` 23 |
| Per-family fixture plan + two setlist songs, tests against the contract texts | `tests/fixtures/families/`, `tests/fixtures/tests/test_family_fixtures.py` |
| Seeding helper (fixture song with every ingredient of every family, three-song setlist), 1.19.0 client, PDF reader, flows | `e2e/support/{handout-seed,insights-api,insights-suite,handout-flow,handout-pdf}.ts` |
| Page objects (`getByTestId` only, ui-test-ids 1.19.0 names) | `e2e/pages/{quality-panel,handout-menu}.ts` |
| Specs (tag `@insights`) | `e2e/tests/{stem-quality,stem-quality-tips,handouts,setlist-handout}.spec.ts`, `e2e/a11y/insights-handouts.a11y.spec.ts` |
| Own E2E pass `insights` (excluded from the default suite) | `e2e/package.json` `test:insights`, `scripts/ci-e2e-pass.sh`, CI matrix, `scripts/tests/test_ci_e2e_split.py` |

The specs and the seeding hooks skip while the stack has no 020 backend (`/api/handouts/options` 404/405), so the
default and feature suites stay green on a stack without the other lanes.

## Evidence

- Fixtures gate (as CI): ruff, ruff format, mypy clean; 328 passed; `generate_family_fixtures.py` 98 % line+branch
  (the uncovered line is `sys.exit(main())`), total 99 %.
- E2E: `npm run lint` 0 warnings, `npm run typecheck` clean, `playwright test --list --grep @insights`: 55 tests in 5
  files (chromium, firefox, a11y); the default selection lists none of them.
- Smoke on an isolated fake stack (project `e2e020`, ports 38080/38081, images built from this branch; the 020
  backend is not on it): the seeding steps all passed in 59 s on the real 014/016 fakes (sections, 42 chord
  segments, lyrics found, the detail split, all eight notation items incl. guitar/bass tabs and the `clarinet-bb`
  part on `other.woodwinds`, the three-song setlist with its notes); worker `/health` shows the `insights` lane;
  `npm run test:insights` on Chromium: 24 skipped (feature skip), 0 failed. Finding fixed: the fake section labels
  differ from the song's form, so the form-chart check reads the stack's sections. Stack torn down with `down -v`.

## Decisions

- PDF facts are read with pypdf from the worker lock (as 014's `practice-flow-pdf.ts`), not pdf-lib: no new npm
  dependency (lane rule 11). Page count, outline, `/Title`, `/Lang`, page text.
- The fixture song is 014's `practice-song.wav` (sections known); two new short songs make the setlist distinct.
- A one-click handout is found as the new handout asset of its track and family that was not there before the click
  (the browser never sees the Blazor server's API call); API-created items are waited on by their own id.
- Reports are waited on by `measuredAt` (the API exposes no job id); "Check again" waits for a report measured after
  the one on screen.
- 020 specs run in their own CI pass `insights` (LESSONS 014/015: the default job was at its limit).

## Readings to confirm at the mid-wave checkpoint (TEST-STRATEGY 23.4)

1. Tips after the fake split: vocals/other `use-detail-stem`, guitar `hard-to-isolate`, piano `not-in-song`,
   drums/bass `clean`; before: vocals/guitar/other `try-detail-separation` (backend T049/T051).
2. "Mute this stem" opens the mixer with `mute-piano` on; "Use the <child> detail stem" opens the mixer with
   `strip-other-<child>` (frontend T053).
3. Rating `aria-label`s contain the rating words; the not-present word is "not in this song" (frontend T046).
4. Handout item names contain the family label; footer "Page n of N" (audio-ingest T068, backend T063).
5. The setlist handout outline has one bookmark per song titled with the track title (audio-ingest T083).

## Open items

- T102 at integration: full default suite + `npm run test:insights` + `--project=a11y --grep @insights` on a real
  fake stack, Chromium and Firefox, retries 0; traceability table is in TEST-STRATEGY 23.5.
- No dependency added.
