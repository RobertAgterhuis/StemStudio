# Help topics and tour (feature 017, help-content 1.0.0 format, no version bump)

Files under `src/StemStudio.Web/Help/topics/mixer/<id>.md` (area `mixer`; no new area, so the
help-content contract stays 1.0.0). `reviewed: "017"`. Every new `data-help-id` below must be
listed in a topic's `anchors` (help test 4 of help-content.md).

| Topic id | Title | Levels | Anchors (`data-help-id`) | Summary (<= 140 chars) | Related |
|---|---|---|---|---|---|
| `practice-eq` | Practice EQ | all | `practice-eq-*`, `strip-has-effects-*` | Focus on one instrument's frequency range, or make room for it in the rest of the song. | `mixer-effects`, `mixer-mute-solo` |
| `mixer-effects` | Effects | advanced, expert | `fx-open-*`, `fx-panel` | EQ, compressor and reverb per strip. Effects are saved with the mix and included in exports. | `effects-eq`, `effects-compressor`, `effects-reverb` |
| `effects-eq` | EQ | advanced, expert | `fx-eq-*` | Six bands: cut rumble, remove boom, add presence. The curve shows what the EQ does. | `practice-eq`, `effects-presets` |
| `effects-compressor` | Compressor | advanced, expert | `fx-comp-*` | Evens out loud and soft parts. Threshold sets where it starts, ratio how strongly it works. | `effects-eq` |
| `effects-reverb` | Reverb | advanced, expert | `fx-reverb-send`, `reverb-kind`, `reverb-return`, `reverb-predelay`, `meter-reverb` | One shared room for the mix; each strip sends some of its sound into it. | `mixer-effects` |
| `effects-presets` | Effect presets | advanced, expert | `fx-preset-*`, `fx-copy`, `fx-paste`, `fx-reset` | Start from a preset, then adjust. "Modified" shows when you changed it. | `practice-eq` |
| `effects-bypass` | Bypass | advanced, expert | `fx-bypass-*`, `fx-bypass-all` | Hear a strip or the whole mix without effects, to compare. Bypass all is not saved. | `mixer-effects` |
| `effects-export` | Export with effects | advanced, expert | `export-effects`, `export-effects-note`, `export-prerender-*`, `asset-effects-*` | Exports from the mixer include effects as you hear them; the browser renders them first. | `mixer-effects` |
| `effects-load` | Effects load | expert | `fx-load`, `fx-number-*` | How much of the audio budget the effects use. Effects on hidden strips are paused. | `mixer-effects` |
| `controllers` | Controllers | advanced, expert | `nav-controllers`, `controllers-*` (route `/controllers`, screen topic) | Use a MIDI controller for faders, mute, solo and transport. Works in Chrome and Edge. | `controllers-learn`, `controllers-troubleshooting` |
| `controllers-learn` | MIDI learn | advanced, expert | `midi-learn-*`, `midi-status`, `midi-bank-indicator` | Click a control, move a knob or fader on your device: they are linked. | `controllers` |
| `controllers-modes` | Binding modes | expert | `controllers-binding-mode-*`, `controllers-binding-invert-*`, `controllers-binding-pickup-*`, `controllers-strips-per-bank-*`, `controllers-monitor` | Absolute, relative encoders, toggle or momentary buttons, pick-up. | `controllers-learn` |
| `controllers-troubleshooting` | Controller not working | all | `controllers-support`, `controllers-connect`, `controllers-level-note` | Browser support, permission and connection problems, and what to do. | `controllers`, `keyboard-shortcuts` |

`keyboard-shortcuts` is the 004/014 shortcut topic (the fallback when MIDI is unavailable); if its
id differs at merge, the CTO fixes the `related` entry.

Glossary additions (`area: glossary`): `eq`, `compressor`, `reverb-send`, `bypass`, `midi`,
`midi-cc` - one-line definitions, levels all.

Tour `practice-eq` (levels beginner; 3 steps): `practice-eq-*` on the first strip ("Choose
Practice EQ") -> `practice-eq-focus-*` ("Focus on one instrument") -> `practice-eq-make-room-*`
("Or make room for your own playing").
