# Hand-off: qa / 015 polish (lane L6, T085, T086) and the qa summary of the lane

## E2E written (typechecked, linted, not run - lane rule)
| Spec | Story | Run in |
|---|---|---|
| `tests/recording.spec.ts` | US1, one case per family + rules + denied | project `recording` |
| `tests/recording-loopback.spec.ts` | US2 (skips without `E2E_RECORDING_LOOPBACK=1`) | `recording`, TestLoopback stack |
| `tests/recording-loop.spec.ts` | US3 | `recording` |
| `tests/recording-compare.spec.ts` | US4 | `recording` |
| `tests/recording-manage.spec.ts` | US5 | `recording` |
| `tests/recording-export.spec.ts` | US6 (`@recording @export`) | `recording` |
| `tests/recording-adjust.spec.ts` | US7 | `recording` |
| `tests/recording-firefox.spec.ts` | T085 Firefox smoke (skips outside Firefox) | `firefox` with `--grep @recording` |
| `a11y/recording.a11y.spec.ts` | T085 axe (both themes, Expert), keyboard-only, live region, reduced motion | `a11y` |
| `visual/recording.spec.ts` | T085 panel baselines, desktop + 320 px, both themes | `visual` (container; CTO makes baselines) |

Shared helpers owned by L6: `pages/recording-panel.ts`, `support/recording.ts`, `support/recording-api.ts`,
`support/recording-test.ts`, `pages/mixer-page.ts` (`MeterTarget`).

`npx tsc --noEmit -p .`: 0 errors; `npx eslint --max-warnings 0` on every recording file: clean.

## Fixtures and strategy
`tests/fixtures/recording/` (T009; 30 tests, 100 % line/branch) and TEST-STRATEGY sections 20 (014) and 21 (015),
updated with the T085 specs.

## Requests for the integration run (T088)
- Generate the recording fixtures first; `E2E_RECORDING_LOOPBACK=1` for the TestLoopback stack.
- The a11y and visual specs launch full Chromium with the fake capture device (worker-scoped launch options); the a11y
  project needs the `microphone` permission, which the specs set themselves.

## Next owner
CTO T088.
