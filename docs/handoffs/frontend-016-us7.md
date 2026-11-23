# Hand-off: frontend (L5) / 016 HO:us7 (T120, T121) and the mid-wave checkpoint fixes

Branch `016-round-lane-l5`. Not pushed, not merged. E2E T122 is lane L2's.

## Tasks

| Task | Red | Green | Result |
|---|---|---|---|
| T120/T121 track page | 08240aab | f84c1065 | `TrackKey` next to `track-bpm`: `track-key` (text = backend display, "(uncertain)" below 50 % unless the display has it, `data-tonic`, `data-mode`, `data-uncertain`; empty with `data-status` = chords status while not ready); `track-key-regions` with `track-key-region-<n>` "Bar 9: D major" at Advanced (`key.details`, `GET key?transpose=0`) |
| T120/T121 mixer | 08240aab | f84c1065 | `MixerKey` in the mixer title: `mixer-key` from `GET key` with the 012 transpose ("D major (original C major, +2)", `data-tonic` = transposed tonic), reloaded only when transpose, written instrument or chord status change; `mixer-key-current` "Now: E major" with `data-region-index`, computed from the region times and the play position (no per-frame interop); `mixer-key-written` "Your part: A major" for the orchestral family with a transposing instrument chosen in the part view (orchestrator decision); `mixer-key-confidence` "81 % (or A minor)" at Advanced |
| help | 08240aab | f84c1065 | topic `song-key` (anchor `song-key`); catalogue `key.view` (B), `key.details` (A) |
| checkpoint 1 | 28fa23df | 03013faa | MIDI of a single drum piece: `ScoreForm` starts with the only allowed profile when the backend suggests none (`drums.kick` -> drums); red test in `MidiActionTests` |
| checkpoint 2 | - | (test only) | Drumless metronome: `TheMetronome_IsRestoredToItsSettingBeforeDrumless(true/false)` passes - see request 1 |
| checkpoint 3 | - | 4950e6bf (earlier) | T106 landed; from now on red tests compile against stubs |

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| StemStudio.Web | `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` | 3787/3787 | 99.06 % line, 92.53 % branch, 99.6 % method |

Build 0 warnings, format clean, `run_all.py` PASS (0 errors, jscpd ran: 455 clones).
WCAG: key texts are plain text next to the tempo; the region list is a labelled list.
Security: no user input; the key spelling uses fixed tables.

## Decisions

1. The current region is computed in Blazor from `KeyViewDto.regions` and the position (same content as the lane host's
   `data-current-key`, which per L1 is '' without a key).
2. `.karaoke-chord` zero width (instruments.css) is intended: the chord is anchored above its word and overflows visibly.

## Requests for the orchestrator

1. Drumless metronome (mid-wave item 2): not a frontend bug as far as I can see. The 010/012 mixer starts with the
   metronome **on** when the beat grid is reliable (`MixerState`: `Enabled = BeatGrid.Reliable`), so on the fake stack
   the "previous setting" is on and a restore keeps it on (FR-032). Line 84 of L2's `instrument-drums.spec.ts` should
   compare with the state read before the first click (or switch the metronome off first). The bUnit test proves both
   directions.

## Open items

Next: T127/T128 (US8 capo), then T135 polish (SIG list of the orchestrator noted).
