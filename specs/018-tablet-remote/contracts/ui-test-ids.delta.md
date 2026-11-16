# Contract delta: UI test ids for feature 018 (1.17.0, additive)

Status: **delta** (018 spec, 2026-11-16). Base: the full `ui-test-ids.md` of feature 017
(**1.16.0**, written in parallel). In Phase 0 the architect merges this delta unchanged into
`specs/018-tablet-remote/contracts/ui-test-ids.md` **1.17.0** (tasks.md T001). Nothing below renames,
removes or changes an existing id; every base rule applies (`getByTestId` only, level visibility,
placement inside 004's layout).

Two surfaces:

- **Main app** (Blazor, `localhost`): ids without prefix, as in the base document.
- **Remote client** (`src/remote-client`, remote listener): ids prefixed **`rc-`**, same rules;
  set as `data-testid` attributes in the DOM the client builds.

Binding for the **frontend** agent; the **qa** agent authors the E2E specs against these names.

## Changelog line (for the merged document)

> 1.17.0 (additive, feature 018) - install button and remote indicator in the shell, Stand view of
> the mixer, the Remote control page (mode, switch, pairing dialog, devices, audit) and the remote
> client (`rc-*`: pairing, connection, transport, stand, mix, songs). All 1.16.0 ids are unchanged.

## Main app ids

| Screen | `data-testid` | Element / attributes | Levels |
|---|---|---|---|
| Shell (every page) | `install-app` | button; exists only while the browser offers installation (Chromium `beforeinstallprompt`) | all |
| Shell | `install-app-hint` | text hint for browsers without an install prompt (Safari: "Share -> Add to Home Screen"); dismissible | all |
| Shell | `remote-indicator` | status chip; exists only while remote mode is effective; attributes `data-connected` (0-4), `data-controlled` = `true` when this tab is the active player | all |
| Navigation | `nav-remote-control` | link to `/remote-control` | Advanced, Expert; Beginner only when remote mode is set up |
| Mixer | `stand-toggle` | button "Stand view" (`aria-pressed`) | all |
| Mixer (stand) | `stand-view` | region of the stand layout; attributes `data-orientation` = `landscape`/`portrait`, `data-family`, `data-content` = `lyrics`/`chords`/`sheet`/`sections` | all |
| Mixer (stand) | `stand-part-picker` | radio group of families with content | all |
| Mixer (stand) | `stand-part-<family>` | one option; `<family>` = `vocals`/`guitar`/`bass`/`keys`/`drums`/`other`; `aria-checked` | all |
| Mixer (stand) | `stand-content-<kind>` | content tab (`lyrics`, `chords`, `sheet`, `sections`); exists only when that kind exists for the family | all |
| Mixer (stand) | `stand-content` | the large content area; attribute `data-page` (sheet page, 1-based) | all |
| Mixer (stand) | `stand-lyric-current` | the current lyric line (when lyrics are shown) | all |
| Mixer (stand) | `stand-chord-current` | the current chord symbol (when chords are shown) | all |
| Mixer (stand) | `stand-section-current` | current section name | all |
| Mixer (stand) | `stand-position` | position slider (`aria-valuenow` seconds, `aria-valuetext` "1:03 of 3:32") | all |
| Mixer (stand) | `stand-play` | play/pause (`aria-pressed`), >= 64 px | all |
| Mixer (stand) | `stand-section-restart`, `stand-section-previous`, `stand-section-next` | section buttons, >= 64 px | all |
| Mixer (stand) | `stand-loop` | loop current section (`aria-pressed`) | all |
| Mixer (stand) | `stand-page-previous`, `stand-page-next` | manual page turn (sheet without score map) | all |
| Mixer (stand) | `stand-wake-lock` | switch "Keep screen on"; attribute `data-state` = `on`/`off`/`unsupported` | all |
| Mixer (stand) | `stand-text-size` | slider 100-200 % | Advanced, Expert |
| Mixer (stand) | `stand-exit` | button back to the mixer | all |
| Mixer | `remote-other-tab` | notice "Remote controls another tab"; exists only when another tab is the active player | all |
| Remote control (`/remote-control`) | `remote-page` | page root (screen help topic `remote-control`) | see nav |
| Remote control | `remote-mode` | mode text; attribute `data-mode` = `off`/`lan-http`/`lan-https`, `data-available` = `true`/`false` | all who see the page |
| Remote control | `remote-not-configured` | explanation + manual link; exists only when mode is `off` | Advanced, Expert |
| Remote control | `remote-problem` | start-up problem of the remote surface (data-model 1.1 `problem`); attribute `data-problem` | all who see the page |
| Remote control | `remote-address` | the remote address (origin) as text and copy button | all who see the page |
| Remote control | `remote-certificate` | certificate expiry; attribute `data-expires-soon` = `true`/`false` (`lan-https`) | Advanced, Expert |
| Remote control | `remote-insecure-warning` | "Not encrypted" warning with HTTPS set-up link (`lan-http`) | all who see the page |
| Remote control | `remote-enabled-switch` | switch "Allow remote control" (`aria-checked`); disabled when mode is `off` | all who see the page |
| Remote control | `remote-pair-start` | button "Pair a device"; disabled while the switch is off or 8 devices are paired | all who see the page |
| Remote control | `remote-pair-dialog` | pairing dialog; attribute `data-status` = PairingStatus wire value | all who see the page |
| Remote control | `remote-pair-code` | the 6-digit code (large, `aria-label` spells the digits) | all who see the page |
| Remote control | `remote-pair-qr` | QR image (SVG, `role="img"`, `aria-label` = "QR code for <address>") | all who see the page |
| Remote control | `remote-pair-url` | address to type when scanning is not possible | all who see the page |
| Remote control | `remote-pair-expires` | countdown (`aria-live="polite"` every 30 s) | all who see the page |
| Remote control | `remote-pair-request` | the request card; attribute `data-device-name` | all who see the page |
| Remote control | `remote-pair-check` | the 3-digit check number | all who see the page |
| Remote control | `remote-pair-allow`, `remote-pair-reject`, `remote-pair-cancel` | buttons | all who see the page |
| Remote control | `remote-pair-stopped` | message "Pairing stopped after 5 wrong codes" | all who see the page |
| Remote control | `remote-device-list` | list of paired devices (`data-count`) | all who see the page |
| Remote control | `remote-device-<deviceId>` | one device row; attributes `data-connected` = `true`/`false`, `data-transport` | all who see the page |
| Remote control | `remote-device-rename-<deviceId>` | rename button (opens an inline field `remote-device-name-input-<deviceId>`) | Advanced, Expert |
| Remote control | `remote-device-remove-<deviceId>` | remove button (confirm dialog of 004) | all who see the page |
| Remote control | `remote-remove-all` | "Remove all devices" (confirm dialog) | all who see the page |
| Remote control | `remote-audit-log` | audit table; rows `remote-audit-<id>` with `data-kind` | Expert |

## Remote client ids (`rc-*`)

| Screen | `data-testid` | Element / attributes |
|---|---|---|
| Any | `rc-root` | app root; attributes `data-secure` = `true`/`false`, `data-mode` |
| Any | `rc-connection` | connection status (polite live region); attribute `data-state` = `connecting`/`connected`/`reconnecting`/`off`/`removed`/`too-many`/`no-player`/`pairing` |
| Any | `rc-insecure-badge` | "Not encrypted" badge (exists only when `secure` is false) |
| Any | `rc-alert` | assertive live region for command rejections |
| Any | `rc-install` | install button (secure context with an install prompt) / `rc-install-hint` (text) |
| Pairing | `rc-pair-form` | form |
| Pairing | `rc-pair-code-input` | 6-digit input (`inputmode="numeric"`, `autocomplete="one-time-code"`); prefilled from the QR fragment |
| Pairing | `rc-pair-name-input` | device name (1-40) |
| Pairing | `rc-pair-submit` | submit |
| Pairing | `rc-pair-check` | check number to compare with the computer |
| Pairing | `rc-pair-status` | `data-status` = `waiting`/`allowed`/`rejected`/`expired`/`invalid`/`rate-limited` |
| Pairing | `rc-pair-again` | button after rejection/expiry/removal |
| Tabs | `rc-tab-transport`, `rc-tab-stand`, `rc-tab-mix`, `rc-tab-songs` | tab buttons (`aria-selected`) |
| Transport | `rc-now-playing` | title and artist of the open song |
| Transport | `rc-no-player` | "No song open on the computer" |
| Transport | `rc-play` | play/pause (`aria-pressed`), >= 64 px |
| Transport | `rc-stop`, `rc-skip-back`, `rc-skip-forward` | buttons (skip = 5 s) |
| Transport | `rc-section-previous`, `rc-section-next`, `rc-section-restart` | buttons |
| Transport | `rc-position` | slider (`aria-valuenow`) |
| Transport | `rc-section-current` | current section |
| Transport | `rc-section-<sectionId>` | section chip (tap = go to; long-press or the loop icon `rc-section-loop-<sectionId>` = loop) |
| Transport | `rc-loop` | loop on/off (`aria-pressed`) |
| Transport | `rc-tempo` | tempo slider 50-120 % with `rc-tempo-slower` / `rc-tempo-faster` (5 % steps) |
| Transport | `rc-metronome`, `rc-count-in` | toggles (`aria-pressed`) |
| Stand | `rc-stand` | stand region; attributes `data-family`, `data-content`, `data-orientation` |
| Stand | `rc-stand-part-<family>` | family option (`aria-checked`) |
| Stand | `rc-stand-content-<kind>` | `lyrics`/`chords`/`sheet`/`sections` |
| Stand | `rc-lyric-line-<index>` | lyric lines; the current one has `aria-current="true"` |
| Stand | `rc-chord-current` | current chord |
| Stand | `rc-sheet` | sheet canvas container; attribute `data-page`, `data-pages` |
| Stand | `rc-sheet-previous`, `rc-sheet-next` | page buttons |
| Stand | `rc-wake-lock` | keep-screen-on switch; `data-state` = `on`/`off`/`unsupported`/`insecure` |
| Stand | `rc-text-size` | text size 100-200 % |
| Stand | `rc-stand-play`, `rc-stand-section-previous`, `rc-stand-section-next`, `rc-stand-loop` | big transport row of the stand tab |
| Mix | `rc-family-practise-<family>` | "Practise my part" per family (`aria-pressed`) |
| Mix | `rc-stem-<stemId>` | stem row with `rc-stem-mute-<stemId>` and `rc-stem-solo-<stemId>` (`aria-pressed`) |
| Mix | `rc-master` | master volume slider (dB) |
| Songs | `rc-setlist` | setlist card with `rc-setlist-previous`, `rc-setlist-next` (disabled at the ends) |
| Songs | `rc-search-input`, `rc-search-results` | library search; results `rc-song-<trackId>` (button opens on the computer) |
| Songs | `rc-waiting-computer` | "Waiting for the computer" while the computer asks about unsaved changes |
