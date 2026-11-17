# Hand-off: frontend 014, HO:us1 (lane L5, round 014+015)

US1 "Loop a section with one click". Builds on HO:foundational (`frontend-014-foundational.md`).

## Tasks (red -> green)

| Task | Commit |
|---|---|
| Typed API client of all 014 operations (sections, score views, setlists, practice log) with route/method/body tests | 446e9d9d |
| T058 failing bUnit tests: section lane and section commands | 43e3d41c |
| T059 `Components/Practice/SectionLane.razor(.cs)`, `MixerState.Sections`, `Mixer.Sections.cs`, section commands, trainer region label | ac795e66 |
| T061 help `mixer-sections`, glossary `section`, `speed-trainer-region`, tour `practice-section` v1 (+ `PracticeFlowHelpTests`) | 5dea6b67 |

## Behaviour

- Lane above the console panels at every level: `section-lane` (`role="list"`, `data-status`, `data-revision`), one
  `section-<id>` button per section (`data-kind`, `data-start`, `data-end`, `data-current`, `data-looped`,
  `aria-pressed` = looped, `aria-current` on the current one). Visible text = label + kind; a visually hidden suffix adds
  the bar range ("bars 21 to 28", 1-based; time range without bars), "playing now" and "looping" (no `aria-label`, so the
  accessible name contains the visible text). Current = thick outline + leading bar; looped = stripes + underline (not
  colour alone, FR-031).
- Status (`section-status`): "Detecting sections..." (none/queued/detecting), the failure reason + `section-retry`,
  "Tempo grid uncertain - boundaries may be off" (warning `grid-unreliable` or `no-grid`), "Detected automatically - check
  and edit" (origin detected). API errors show `section-error`.
- First open with status `none` calls `detectSections` once, then polls (004 `PollingComponent`) while queued/detecting.
- Click loops the section (`LoopRegionDto.SectionId` set, loop on) and seeks to its start when the playhead is outside.
  Moving A or B by hand drops the section; turning the loop off and on over the same markers keeps it.
- `section-previous` / `section-restart` / `section-next` / `section-loop-current` send the same command ids as the keys
  (`mixer.section.*`, `mixer.loop.section`) through the mixer's `MixerCommandRegistry`.
- Trainer panel (Advanced+): `trainer-region-label` "Region: Guitar solo (0:40-0:56)" or "Region: A-B loop (...)".
- New stylesheet `css/components/practiceflow.css` (tokens only), loaded before `care` (013's test pins `care` last).

## Quality

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Web (Release, `-p:CollectCoverage=true`) | 3135 passed | 99.43 % | 93.54 % |
| web-interop (unchanged since HO:foundational) | 412 passed | 99.8 % | 98.29 % |

- Release build 0 warnings; `dotnet format --verify-no-changes` clean except L6's `RecordingApiClientTests.cs:103`
  (reported, L6's).
- `tools/quality/run_all.py`: PASS, 0 errors, jscpd ran (369 clones, none mine as errors).
- a11y (bUnit): list of buttons inside `<li>`, every control named, name contains visible text, targets >= `--ss-target-min`.
- No untrusted input added (API responses only). No new dependencies.

## Open / notes

- E2E `sections.spec.ts` is L3's; the ids above follow ui-test-ids 1.13.0.
- `section-edit`, `section-detect-again`, `section-confidence-*` come with US3 (T072/T073) through the lane's
  `Actions` / `ItemExtra` slots.
