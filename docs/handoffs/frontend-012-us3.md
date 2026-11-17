# Hand-off: frontend / 012 US3 (T059, T060)

Branch `012-lane-frontend`. Contracts: ui-test-ids 1.11.0 `countin-*`, audio-engine 2.3.0 "Count-in" /
`PracticeStatus.countIn`, data-model 3.3 and 5.

## What was built

| Pair | Red | Green |
|---|---|---|
| T059/T060 | dabecad2 | 844037df |

- `Components/Mixer/Practice/CountInControl.razor(.cs)` in the practice panel: `countin-toggle` (`role="switch"`,
  help marker `practice-count-in`; Beginner turns on 1 bar, Advanced/Expert the remembered bar count), `countin-bars`
  (select 1 / 2, Advanced+, catalogue `practice.count-in.options`; choosing a count turns the count-in on),
  `countin-every-pass` (checkbox "Before every loop pass", disabled while no usable loop, `aria-describedby` -> the
  `trainer-needs-loop` text), `countin-unavailable` "Count-in needs a beat grid or a manual BPM." (tempo rule =
  `PracticeRules.TrackBpm`: manual BPM or reliable grid; updates at once when a BPM is typed). The count-in stays
  usable while the stretch stage is unavailable.
- `Components/Mixer/Practice/CountInIndicator.razor(.cs)` in a new `.ss-countin-slot` of the mixer dock (between the
  karaoke banner and the transport): `countin-indicator` only while `countIn.active`, `role="status"`,
  `aria-live="polite"`, `data-beats-remaining/-total`, a large number and `countin-dot-<n>` (`done`/`current`/`todo`),
  all `aria-hidden`. A persistent sr-only announcer (`[data-countin-announcer]`, `role="status"`) says "Count-in" when
  a count-in starts and "Go" when it ends - never per beat.
- No flashing: the current beat gets a ring and a small scale, a beat is never re-lit, no CSS animation; the scale is
  off with `data-motion="reduced"` and with the OS reduced-motion setting.
- `mixer.countin.toggle` (key C) toggles like the switch; turning on is ignored without a tempo.
- Help: `practice-count-in`, glossary `count-in`, related link and a sentence in `mixer-metronome`.
- Tests: `CountInControlTests` (12), `CountInIndicatorTests` (10); the level lists in `PracticePanelTests` now include
  the count-in and trainer ids; `KaraokeMixerTests` dock order is karaoke, count-in slot, transport.

## Decisions (owner unavailable)

1. **Announcements via a persistent live region** instead of the indicator itself: the indicator appears and
   disappears with the count-in, and a region inserted with its text is not reliably read, nor can a removed region
   say "Go". The indicator keeps `role="status"`/`aria-live` from the contract but has no changing accessible text.
2. Choosing a bar count switches the count-in on (a musician picking "2 bars" expects it to count).
3. An active count-in / every pass can always be turned off, even without a tempo or a loop.

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 2050, Skipped: 0, Total: 2050
| StemStudio.Web | 99.6% line | 93.66% branch | 99.71% method |   gate 90/90 met
```

## Quality checks

`dotnet build` 0 warnings; `dotnet format --verify-no-changes` clean; `tools/quality/run_all.py`:
`2255 files, 17724 units, 259 clones, 3 exemptions, 0 errors, 824 warnings -> exit 0` (a 5-parameter theory found on
the way was turned into a 2-parameter one).

## Dependencies added

None.

## Docs impact

User manual (mixer): count-in - 1 or 2 bars, accent on beat 1, follows the beat grid or manual BPM and the speed, uses
the click volume even with the click off, before every loop pass (Advanced), cancelled by pause/stop/seek, needs a
tempo, dots above the transport, key C.

## Open issues / requests

- audio-engine: the indicator relies on one `OnPracticeChanged` per beat with `countIn.active` and `beatsRemaining`
  (N..1), and a final one with `active: false` when the music enters (that one triggers "Go").
- qa: the dots are `[data-testid=countin-dot-<n>]` inside `countin-indicator`; the announcer has no test id
  (`[data-countin-announcer]`).

## Next owner

frontend (polish: T064, T065).
