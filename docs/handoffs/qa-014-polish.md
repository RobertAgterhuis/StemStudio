# QA 014 - HO:polish (lane L3)

E2E specs written against ui-test-ids 1.13.0 / backend 1.13.0; `npx tsc --noEmit` and `npx eslint . --max-warnings 0`
clean in `e2e/`; not run (lanes start no stack). Tag `@practice-flow` on every new spec.

| Task | Spec | Page objects / support |
|---|---|---|
| T060 | `e2e/tests/sections.spec.ts` | `pages/section-lane.ts`, `support/practice-flow.ts` |
| T074 | `e2e/tests/sections-edit.spec.ts` | `pages/section-editor.ts` |
| T084 | `e2e/tests/score-follow.spec.ts` | `pages/score-panel.ts` |
| T094 | `e2e/tests/setlists.spec.ts` | `pages/setlists-page.ts` (list, editor and the setlist bar), `support/practice-flow-pdf.ts` |
| T100 | `e2e/tests/practice-log.spec.ts` | `pages/practice-log-panel.ts` (panel and `/practice`) |
| T068 | `e2e/tests/shortcuts-pedals.spec.ts` | `pages/shortcut-settings.ts` |
| 012 key move | `e2e/tests/practice-levels.spec.ts` | presses `0` (reset) and `Shift+K` (count-in) instead of `r` / `c` |

Notes for the integration run:
- Sections scenario 4 needs a stack with `StemStudio__Practice__SectionDetectOnComplete=false` and
  `E2E_SECTION_DETECT_ON_COMPLETE=false` (skipped otherwise); scenario 5 uses `silence_5s.wav` until L2's T018 fixture.
- `setlists.spec.ts` reads the PDF with pypdf through `uv run --project worker python -I` (no shell; bytes on stdin):
  `uv` and the worker lock must be available on the E2E host.
- The setlist page objects do not rely on `setlist-bar` `data-position` being 0- or 1-based (the contract does not say);
  they check the mixer URL and the bar. `score-select` is chosen by option text "<Profile> - <name>" (ui-test-ids).
- The practice-log spec cannot see the API's 409 (`practice-log-off`) in the browser network log: the Blazor Server web
  app calls the API server-side. It checks that no session is stored instead, and that every browser request goes to the
  web app's own origin (SC-009).
- `score-follow.spec.ts` has no Chromium performance trace for SC-004 (2 ms per frame); that needs the integration's
  perf run (it measures the engine, not the API).
- `shortcuts-pedals.spec.ts` covers the FR-020 commands by group; `mixer.save` / `save-as` (Ctrl+S) are covered by the
  existing 004 specs and not repeated.
