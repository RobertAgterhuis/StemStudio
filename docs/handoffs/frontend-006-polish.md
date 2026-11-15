# Hand-off: frontend / 006 polish (T060) and feature summary

## What was built

- **Accessibility (T060)**:
  - The banner host stays `aria-live="off"` (`role="group"`, `aria-label="Lyrics banner"`).
  - **Show full lyrics** (`karaoke-full-lyrics`, `aria-expanded` + `aria-controls`) shows every line as an `<ol>`
    (`karaoke-full-lyrics-text`).
  - Every banner control has a name and is in DOM order before the host (toggle, size, offset, reset, help hint,
    full lyrics). The offset keys are ±50 ms.
  - `karaoke-unavailable` is `role="status"`.
  - The untimed preview is a named, focusable region so it can be scrolled with the keyboard.
  - The paste textarea has a `<label for>`.
  - Contrast is proven for ink, muted and the vocals colour on the banner in both themes (US3 test).
- **Help review**: `karaoke` (countdown, toggle remembered, reduced motion, full lyrics, copyright), `karaoke-offset`
  (range, arrow keys, reset), `lyrics-sources` (LRCLIB, what is sent, statuses, copyright), `lyrics-align` (GPU/CPU,
  the 1.6 GB model, cancel, try again). Topic ids have no `help-` prefix, as in ui-test-ids 1.6.0.
- **Feature summary** (see `frontend-006-{us1,us2,us3}.md`):
  - engine 2.1.0 interop;
  - the karaoke banner docked above the transport;
  - the lyrics card on the track page;
  - the 7 lyrics client methods;
  - catalogue entries `karaoke.size`, `karaoke.offset`, `karaoke.timing-origin`, `lyrics.align-device`,
    `lyrics.refine`, `lyrics.remove`;
  - CSS `karaoke.css` and `lyrics.css`;
  - the `stemstudio.karaoke` local-storage key.

## Red -> green evidence (feature 006)

| Task pair | Red | Green |
|---|---|---|
| T037 / T038 | 2baf69b | f17ebb9 |
| T046 / T047 | 4a69ea4 | 32ad025 |
| T053 / T054 | aed7237 | 2eb4daa |
| T060 | 0a9f9b1 | af27f4b |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1163, Total: 1163   (1003 before 006)
| StemStudio.Web | 99.5% line | 93.31% branch | 99.89% method |
```

## Quality checks

- `dotnet build` 0 warnings (TreatWarningsAsErrors).
- `dotnet format StemStudio.slnx --verify-no-changes`: clean.
- `tools/quality/run_all.py`: 1286 files, 0 errors, exit 0. Warnings only in test files plus the older Web
  warnings; no new production warning.
- `css_tokens_check.py`: 0 raw colours.

## Dependencies added

None.

## Open issues / requests

- **architect** (ui-test-ids 1.6.x, additive): please adopt the extra ids `karaoke-full-lyrics`,
  `karaoke-full-lyrics-text`, `lyrics-paste-error` and `lyrics-align-device-fallback`.
- `docs/public` does not exist on this branch, so there is no manual update.

## Notes for qa

- `karaoke-unavailable` replaces the host (host count 0) and is absent while the banner is hidden.
- Banner choices live in `localStorage["stemstudio.karaoke"]`.
- Levels: Beginner has no `karaoke-size`, `karaoke-offset`, `karaoke-offset-reset`, `lyrics-remove` or
  `lyrics-align-device`; Expert adds `.ss-karaoke-origin` and `lyrics-align` for line-timed lyrics.

## Next owner

frontend (008 US1: T043/T044); qa 006 T039/T048/T055; CTO 006 T064+.
