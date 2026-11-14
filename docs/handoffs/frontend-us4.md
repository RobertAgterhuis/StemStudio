# Hand-off: frontend / us4

## What was built

- **Mixer save/load** (`Components/Mixer/Mixer.Save.cs`, `SaveMixDialog.razor`, `MixerState.Mix.cs`, `MixNames`):
  - `mix-save` on a mix that was never saved opens the name dialog, the same as `mix-save-as` (qa convention 3).
    On a loaded or saved mix it sends a `PUT` with the current name.
  - The dialog (`mix-name-input` with oninput binding, `mix-save-confirm`, `mix-save-cancel`) checks that the
    trimmed name is 1-100 characters and then sends a `POST`.
  - A 409 shows "The name "X" is taken for this track. Choose another name." in `mix-error` inside the dialog.
    Other failures show the API message. A failed `PUT` keeps the mixer dirty and shows `mix-error` on the page.
  - A successful save clears the dirty flag and shows the name (`mix-name`). After the first save the URL is
    replaced with `?mixId=<new id>`, so a reload restores that mix.
- **Loading `?mixId=`**: `GET /api/mixes/{id}`. `MixerState.ApplyMix` restores channels (stems without an entry get
  defaults), master, metronome and the loop, including markers and the toggle, and clears the dirty flag. The full
  mix then goes to the engine after `loadTrack`, meters and channels, master, metronome and loop (CTO note 4).
  Switching to another mix while the engine is ready re-applies the full mix without reloading the track. A mix
  that belongs to another track, or a 404, shows `mix-error`, and the defaults stay in place.
- **Mix library** `/mixes` (`Components/Pages/Mixes.razor` + `.razor.cs`):
  - The rows (`mix-row-<id>`) show the name, the track title and the last-modified time (`<time datetime>` in UTC).
  - `mix-load-<id>` opens the mixer with `?mixId=`.
  - `mix-duplicate-<id>` sends a `POST .../duplicate` and refreshes the list.
  - `mix-rename-<id>` opens the same name dialog, does `GET` + `PUT` with the new name, and shows the 409 message.
  - `mix-delete-<id>` asks for confirmation in the Radzen dialog (`confirm-dialog-ok` / `-cancel`) with the text
    "The track and its stems remain." and then sends a `DELETE`.
  - Empty, loading and error (`mixes-error`) states.
- The `e2e/pages/mix-library-page.ts` and `mixer-page.ts` (save) selectors all exist.

## Red -> green evidence

| Task pair | Red | Green |
|---|---|---|
| T104 -> T105 | c05e3b1 (does not compile: `MixNames`, `ApplyMix`, `ToSettings` missing) | cdd0456 |
| T106 -> T138 | c05e3b1 (`Mixes` page missing) | cdd0456 |

## Tests and coverage

`dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true`:
`Passed! - Failed: 0, Passed: 179, Skipped: 0, Total: 179`; `StemStudio.Web` line 99.76 %, branch 94.29 %,
method 100 %. The gate passes.

## Quality checks

`dotnet build` 0 warnings; `dotnet format StemStudio.slnx --verify-no-changes` exit 0.

## Dependencies added

None.

## Open issues / requests

- The name dialog is an in-page modal (`role="dialog"`, `aria-modal`). Confirmations use the Radzen
  `DialogService`. Extra ids (additive): `mix-save-cancel`, `mix-name`, `mixes-error`, `mixes-empty`.
- `mix-error` appears both in the mixer and in the library rename dialog. It is never rendered twice at once.

## Next owner

CTO review; frontend continues with polish (T112).
