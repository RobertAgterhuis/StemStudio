# Hand-off: qa / us1 (T081)

## What was built

`e2e/tests/import.spec.ts` (authored in the wave, executed in the integration wave T122):

- legal notice ("personal practice", "copyright") and `stem-limitation-help` visible (FR-005, FR-010);
- upload `music_100bpm_20s.mp3` -> `track-status` completed, `track-progress` `aria-valuenow=100`,
  `track-model` "fake", `track-stem-<name>` for all six stems, `track-bpm` within 3 of 100;
- re-upload of a processed file -> same track id, completed within 2 s (SC-006);
- streamed upload of `upload_4mb.wav` completes, duration ~24 s via the API (G1);
- `corrupt.mp3` -> failed + non-empty `track-error`; `track-retry` re-queues (API `updatedAt`
  changes) and ends failed again with a reason (US1-8);
- `/tracks` row -> track detail -> `open-mixer` -> `/tracks/{id}/mixer` with strips (FR-018, G6);
- rejects (SC-007): `unsupported.ogg`, a `vimeo.com` URL, a YouTube playlist URL each show
  `import-error` with a reason, stay on `/`, and the API track count is unchanged.

The spec is rerun-safe: uploads are cached by SHA-256, so set-up goes through
`api.ensureCompleted()` (200 or 202, retry once if a previous run left the track failed).

## Red -> green evidence

E2E specs are behavioural and go green only against the merged stack (tasks.md cross-owner rule).
Red today against the skeleton (Api + Web on 18081/18080), proving the harness and both
browsers run:

```text
$ npx playwright test tests/import.spec.ts -g "notice" --reporter=line
    TimeoutError: locator.waitFor: Timeout 15000ms exceeded.
      - waiting for getByTestId('legal-notice') to be visible        (chromium)
    TimeoutError: locator.waitFor: Timeout 15000ms exceeded.
      - waiting for getByTestId('legal-notice') to be visible        (firefox)
  2 failed
```

Commit: `0cb995c`. Green evidence follows in T122.

## Files changed

`e2e/tests/import.spec.ts`; `e2e/playwright.config.ts` (`actionTimeout` 15 s,
`navigationTimeout` 30 s so missing ids fail fast).

## Tests and coverage

`npx tsc --noEmit` clean, `npm run lint` clean. Coverage: n/a (E2E). Fixture generator unchanged
(100 % line / 100 % branch, see qa-foundational.md).

## Quality checks

ESLint strict type-checked + eslint-plugin-playwright (no conditionals in tests, no fixed waits,
no `force`), complexity <= 5, <= 4 params; tests <= 60 lines per function.

## Dependencies added

None.

## Open issues / requests

- **frontend**: the import must start when a file is selected on `import-file-input` (there is no
  submit id for files) and navigate to `/tracks/{id}` on 200/202 (T076 already says so).
- **frontend**: `track-row-<id>` should contain a link (or be clickable) to the track detail.
- **architect**: consider pinning in ui-test-ids.md that `track-status` contains the contract
  status word (`completed`, `failed`, ...); the suite matches it case-insensitively.

## Next owner

qa (T097). CTO: run in T122.
