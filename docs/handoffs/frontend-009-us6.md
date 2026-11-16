# Hand-off: frontend / 009 US6 (clear behaviour without a GPU)

Tasks **009:T055 -> T056** (ticked). Commits: red 3a8319c -> green fde9c17.

## What was built

- `GenerationStatusBanner` (`role="status"`, `data-testid="generation-status"`, `data-state` =
  `notInstalled`/`unavailable`/`ready`, marker `generation-gpu-required`): headline, the reason
  `generation-status-reason` (element **id** `generation-status-reason`; API reason or "Generation is not available on
  this computer."), `generation-download-notice` ("First use downloads about 4.5 GB of model files.") when ready with
  `downloadBytesNeeded > 0`, and the button `generation-help-link` (opens topic `generation-gpu-required`).
- Shown on the track page section `track-generations`, on `/styles` and on `/generations` (once per page).
- `track-restyle` is rendered for **every** track (the section moved out of the completed-only block) and is
  `disabled` with `aria-describedby="generation-status-reason"` when generation is not ready, or
  `aria-describedby="track-restyle-reason"` ("Restyle is possible once the track is separated.") for a track that is
  not completed. The style library stays fully usable.
- `GenerationStatusSource` unit-tested: 10 s cache on the `TimeProvider`, API failure -> `notInstalled` with
  "The generation status could not be read: <reason>".
- Help topic `generation-gpu-required` (states, how to start the generator, the first download, what keeps working).

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1305, Total: 1305
| StemStudio.Web | 99.13% line | 93.43% branch |
```

## Quality checks

Build 0 warnings; format clean; `sig_check src/StemStudio.Web`: 0 errors.

## Dependencies added

None.

## Open issues / requests

- qa: the status is read when a page or section is first rendered (and at most every 10 s per circuit); a page does not
  re-poll the status on its own. A page reload shows a changed state.

## Next owner

frontend continues with 009 US3 (T060/T061).
