# Hand-off: frontend 014, HO:us5 (lane L5, round 014+015)

US5 "Setlists".

## Tasks (red -> green)

| Task | Commit |
|---|---|
| T092 failing tests `SetlistsTests`, `SetlistBarTests`, `AddToSetlistTests` | a91f4cf4 |
| T093 setlists, editor, PDF dialog, play mode, setlist bar, rail link, Add to setlist | 0e65728d |
| T095 help `setlists`, `setlist-edit`, `setlist-play`, `setlist-pdf` | 099d0110 |

## Behaviour

- `/setlists` (`setlists-page`, rail `nav-setlists` after Library): `setlist-create` opens the name form
  (`setlist-create-name`, `setlist-create-confirm`) and then the new setlist's editor; rows `setlist-row-<id>` with
  `setlist-open-<id>` (link), `setlist-play-<id>` (disabled without songs), `setlist-delete-<id>` (confirmation
  `setlist-delete-confirm` / `-cancel`); `setlists-empty`.
- `/setlists/{id}`: `setlist-name` (saved on change, blank ignored), `setlist-note` and per entry
  `setlist-entry-mix-<entryId>` (mixes of the track, loaded on focus) and `setlist-entry-note-<entryId>` at Advanced+
  (FeatureCatalog `setlists.notes`), `setlist-total` ("2 songs, 7:46"), entries `setlist-entry-<entryId>`
  (`data-position`, draggable) with `-up-`, `-down-`, `-remove-`; `setlist-add` -> `setlist-add-dialog` (completed tracks
  only, `setlist-add-track-<trackId>` radios, `setlist-add-mix` at Advanced+, `setlist-add-confirm`);
  `setlist-removed-note` with OK (`getSetlist?acknowledgeRemoved=true`); every edit is one `replaceSetlistEntries` /
  `updateSetlist`; a failed save shows `setlist-save-error` and reloads the stored setlist. Play button reuses
  `setlist-play-<id>`.
- PDF: Advanced+ (`setlists.pdf-options`) `setlist-pdf` opens `setlist-pdf-dialog` with `-lyrics`, `-sections`,
  `-notes`, `-page-break` and the `setlist-pdf-download` link (`ApiRoutes.SetlistPdf` on the public API base);
  Beginner `setlist-pdf` is the download link with the defaults; an empty setlist shows `setlist-pdf-error`.
- Play mode `/setlists/{id}/play/{n}`: `setlist-bar` (`data-setlist-id`, `data-position`, `data-count`,
  `setlist-bar-note`, `-previous`, `-next`, `-exit`) above a fresh `Mixer` per entry (`@key` = entry id) loading the
  entry's mix through the new `Mixer.SetlistMixId` parameter. The page registers the `setlist` key map
  (Ctrl+Right/Left, pedal preset "next-previous-song"). Moves are internal navigations, so the mixer's
  `NavigationLock` asks about unsaved changes (FR-017).
- Add to setlist: `add-to-setlist-<trackId>` on the track page (completed tracks) and `add-to-setlist-mix-<mixId>` on the
  mixes page: pick a setlist, the song is appended (toast; refused at 100 songs).

## Decision (help)

- The editor route `/setlists/{id}` needs a screen topic (004 coverage rule: one per page), so a fourth topic
  `setlist-edit` was added next to data-model 6's `setlists`, `setlist-play`, `setlist-pdf`.

## Quality

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Web (Release, coverage) | 3394 passed | 99.43 % | 93.39 % |
| web-interop | 412 passed | 99.8 % | 98.29 % |

Release build 0 warnings; format clean except L6's `RecordingApiClientTests.cs:103`; `run_all.py` PASS (0 errors,
jscpd ran); SIG warning `AddToSetlist.AddAsync` 20 NLOC. The rail gets one more link; 013 made the rail scroll on short
windows, so Settings and Help stay reachable at 720 px height and in the 320 px bottom bar.
