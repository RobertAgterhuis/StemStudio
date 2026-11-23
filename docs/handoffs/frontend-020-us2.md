# Hand-off: frontend / 020 HO:us2 (T053, T054)

Branch `020-lane-frontend`. Not pushed, not merged.

## Tasks

| Task | Red | Green | Result |
|---|---|---|---|
| T053/T054 | 287a3c91 | 2e5afa45 | `TipList` buttons (`quality-tip-action-<channel>-<code>`, texts "Split further" / "Open detail stem" / "Mute", disabled while the action runs, at most two tips); `TrackDetail` implements `ITipActions` (`TrackDetail.Quality.cs`): `split-detail` -> `StartDetailAsync` (002 start, same path as the detail card), `open-detail` -> `tracks/{id}/mixer?expand=<parent>`, `mute` -> `tracks/{id}/mixer?mute=<channel>` (`Insights/TipLinks`); mixer query parameters `mute` (applied to the live state before the engine starts: unsaved, no API call) and `expand` (expanded through the existing view-expansion path once the engine is ready) in `Mixer.Quality.cs` |

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| StemStudio.Web | `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` | 3950/3950 | 99.11 % line, 92.43 % branch (gate 90/90) |

`dotnet build` 0 warnings; format clean; `run_all.py` PASS (0 errors; the only 020 finding is the client-test fixture clone
warning of HO:us1).

## Accessibility

Tip buttons are native `button` elements (Radzen), keyboard operable (Enter/Space, no negative tabindex), named by their
visible text; the tips are a plain list inside the stem row (no listbox).

## Decisions

1. Orchestrator Q2: Mute = mixer link with the stem muted in the live mix; nothing is saved and no mix is changed.
2. Orchestrator Q3: open-detail = mixer link that expands the parent (`action.parent`, falling back to
   `action.channel`); the `child` is not needed to show it, because expanding shows every detail stem of the parent.
3. An unknown channel or a parent without detail stems in the link is ignored; a track that is not ready shows the
   mixer's usual not-ready message.
4. "No GPU tip" (FR-006, SC-003) is the backend's rule; the UI renders tips verbatim and the test asserts no tip text
   contains "GPU".

## Deviations

None.

## Open items

None for this checkpoint. Next: US3 (T076-T079).
