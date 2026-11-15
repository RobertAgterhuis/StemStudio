# Hand-off: frontend / 008 US2 (key, rhythm, tempo)

Tasks 008:T048 (red) and T049 (green).

## What was built

- `score-resolution`: checkbox "Simplified rhythm (eighth notes)", shown at every level. It is on by default for the
  vocals profile and is reset when the profile changes. The request always carries `eighth` or `sixteenth`.
- `score-key-override` (Advanced+, feature `score.key-override`, marker `sheet-music-key-tempo`):
  - first option "Automatic (estimated from the notes)" (value empty, sent as null), then the 30
    `ContractLimits.KeyNames`;
  - labels via `ScoreText.KeyName` ("E minor", "B-flat major", "F-sharp minor"), values in music21 spelling;
  - hidden for the drums profile.
- **Tempo** (`Scores/TempoInput`, `Components/Scores/ScoreTempoFields`):
  - When `gridStatus` is `unreliable` or `none`, the dialog shows `score-tempo-required` (marker
    `sheet-music-key-tempo`) and `score-tempo-bpm`, `score-tempo-beats-per-bar` and `score-tempo-first-downbeat` at
    **every** level (ui-test-ids 1.7.0 exception).
  - These fields are prefilled from the track's detected beat grid (bpm, beats per bar, first downbeat) when it has
    one.
  - With a reliable grid the same fields are an optional, empty Advanced override (feature `score.manual-tempo`).
    Empty means the grid is used; partly filled gives a readable error.
  - Checks: 30..300 BPM, 1..12 whole beats per bar, first downbeat 0..900 s.
- **Help**: `tracks/sheet-music-key-tempo.md` (feature `score.key-override`). The title avoids "tempo" so the
  existing help search still finds `bpm` first for "tempo".

## Deviation

- T048 asks to prefill the tempo "from the mixer's metronome override when available". The dialog opens on the track
  page and in the library, not in the mixer, and the override lives only in saved mixes. The fields are therefore
  prefilled from the track's detected beat grid (`TrackDto.BeatGrid`), even when it is unreliable, which is the
  same tempo the metronome starts from.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T048 / T049 | dccccc4 (16 failing) | 7da27f6 |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1203, Total: 1203
| StemStudio.Web | 99.42% line | 92.84% branch | 99.7% method |
```

## Quality checks

- Build: 0 warnings. `dotnet format --verify-no-changes`: clean.
- `sig_check.py src/StemStudio.Web`: 0 errors. No new warning after splitting `TempoInput.Validate`.
- `css_tokens_check.py`: 0 errors.

## Dependencies added

None.

## Notes for qa

- Select the key by its label "E minor": the label and the value are the same for natural keys.

## Next owner

frontend (008 US3: T054/T055); qa T050.
