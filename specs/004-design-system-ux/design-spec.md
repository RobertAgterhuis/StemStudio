# Design specification: StemStudio design system 1.0

Binding for the **frontend** agent (implementation) and **qa** (visual baselines, a11y). Rationale:
[research.md](research.md) R-01..R-07; decisions: ADR-013. Requirements: [spec.md](spec.md).
Values marked *(spike)* are confirmed or completed by task T012.

Contents: 1 Direction - 2 Tokens - 3 Component inventory - 4 Screens and wireframes per level -
5 Feature catalogue (levels) - 6 Keyboard map - 7 Help content inventory - 8 Voice and copy -
9 Accessibility rules.

---

## 1. Direction: "Colour belongs to the music"

A rehearsal-room console, not a marketing site. Chrome is quiet graphite (dark) or cool paper
(light) without a brand hue; the six stems own the colour. One signature: **one strand becomes
six** (a grey line splitting into the stem colours) for separation progress, track ribbons and the
logo mark. Channel strips carry coloured name tabs like masking tape on a desk. One type family
(Archivo) whose width axis does the work: condensed on strips, normal for text, expanded for
titles. Motion only answers the user or shows progress. Radius encodes containment.

What the memorable thing is: the stem colours and the split. Everything else stays disciplined.

## 2. Tokens

All tokens are CSS custom properties in `src/StemStudio.Web/wwwroot/css/tokens.css`, defined
under `html[data-theme="dark"]` and `html[data-theme="light"]` (density and motion under
`html[data-density=...]`, `html[data-motion=...]`). Nothing outside `tokens.css` may contain a
literal colour (guard: `tools/quality/css_tokens_check.py`).

### 2.1 Colour

| Token | Dark | Light | Use / contrast rule |
|---|---|---|---|
| `--ss-color-base` | `#141A24` | `#E6EAF0` | page background, `html`/`body` |
| `--ss-color-surface` | `#1C2431` | `#F7F9FC` | panels, cards, strips |
| `--ss-color-raised` | `#252F3F` | `#FFFFFF` | inputs, help panel, palette, dialogs, popovers |
| `--ss-color-sunken` | `#10151D` | `#DCE1E8` | meter wells, fader tracks, code |
| `--ss-color-ink` | `#E9EDF3` | `#172030` | body text (>= 11.4:1 on every surface) |
| `--ss-color-muted` | `#A3ADBD` | `#4F5B6E` | secondary text (>= 5.9:1 / >= 5.7:1) |
| `--ss-color-divider` | `#2E3949` | `#CBD2DC` | decorative separators only (no contrast duty) |
| `--ss-color-control-border` | `#6B7A93` | `#6E7B90` | input, slider, toggle boundaries (>= 3:1, WCAG 1.4.11) |
| `--ss-color-focus` | `#FFB547` | `#9A5B00` | focus ring 2 px + 2 px offset (>= 3:1 against adjacent colours) |
| `--ss-color-primary` | `= ink` | `= ink` | primary button fill |
| `--ss-color-on-primary` | `= base` | `= surface` | primary button text (14.9:1 / 15.5:1) |
| `--ss-color-success` | `#5FD38D` | `#1E7A45` | completed, loop/click on |
| `--ss-color-warning` | `#F6C944` | `#8A6200` | notices, meter -6..-1 dBFS |
| `--ss-color-danger` | `#FF7A70` | `#B3261E` | errors, destructive, clip lamp |
| `--ss-color-scrim` | `rgb(8 11 16 / 0.6)` | `rgb(23 32 48 / 0.35)` | tour dimming, overlay drawer |
| `--ss-stem-vocals` | `#F28AB3` | `#B8336A` | stem identity |
| `--ss-stem-drums` | `#F5A04E` | `#A85400` | |
| `--ss-stem-bass` | `#A493F5` | `#5B45C9` | |
| `--ss-stem-guitar` | `#3FCBB4` | `#00806D` | |
| `--ss-stem-piano` | `#6FB9F7` | `#1F6FB5` | |
| `--ss-stem-other` | `#B4CE73` | `#5B7417` | |

Stem colours: >= 5.1:1 on dark `surface`/`raised`, >= 4.6:1 on light `surface`/`raised` (so usable
as text there), >= 4.0:1 on light `base` (graphics only there). Detail stems use the parent colour
plus a **pattern index** (tab underline solid / dashed / dotted / double, then repeat), never a new
hue. Meter: fill `success` below -6 dBFS, `warning` -6..-1, `danger` above -1 and the clip lamp.
The token contrast test (task T019) asserts every pair named in this table in both themes.

### 2.2 Typography

Family `--ss-font: "Archivo", "Segoe UI Variable", system-ui, sans-serif` (one variable woff2,
Latin subset, `font-display: swap`, preloaded). Numbers: `--ss-num: tabular-nums` applied to
time, BPM, dB, percent, progress.

| Token | Size / line height | Weight | Width (`font-stretch`) | Use |
|---|---|---|---|---|
| `--ss-type-display` | 33.2 / 1.1 | 750 | 116 % | page titles (h1) |
| `--ss-type-title` | 23 / 1.2 | 650 | 108 % | section titles (h2), dialog titles |
| `--ss-type-heading` | 19.2 / 1.25 | 600 | 100 % | panel headings (h3) |
| `--ss-type-body` | 16 / 1.5 | 400 | 100 % | text, help body |
| `--ss-type-label` | 13.3 / 1.3 | 550 | 100 % | form labels, buttons |
| `--ss-type-strip` | 13.3 / 1.1 | 650 | 75 % | channel tabs, dense table cells |
| `--ss-type-small` | 12 / 1.35 | 450 | 100 % | captions, kbd hints |
| `--ss-type-counter` | 27.6 / 1 | 500 | 90 % | transport position (tabular) |

Sentence case everywhere. No all-caps labels, no eyebrow text above headings. Help panel body
measure 40-64 characters; page text <= 72.

### 2.3 Space, density, layout

- Spacing scale (4 px grid): `--ss-space-1` 4, `-2` 8, `-3` 12, `-4` 16, `-5` 24, `-6` 32, `-7` 48,
  `-8` 64 px.
- Density: `comfortable` (default Beginner/Advanced) and `compact` (default Expert) change only
  `--ss-density` (1 / 0.75), which multiplies control heights (40 -> 32 px) and strip widths
  (96 -> 76 px); minimum target 24 x 24 px always (WCAG 2.5.8).
- Breakpoints: `sm` < 768 px (rail becomes bottom bar, help panel full-screen sheet), `md`
  768-1279 (help panel overlays), `lg` >= 1280 (help panel docks and content reflows).
- Grid: 12 columns, 24 px gutters, content max width 1280 px, left-aligned; mixer full-bleed.

### 2.4 Radius and elevation (radius encodes containment)

| Token | Value | Applies to |
|---|---|---|
| `--ss-radius-control` | 2 px | buttons, inputs, faders caps, M/S |
| `--ss-radius-panel` | 6 px | cards, strips, sections, table container |
| `--ss-radius-float` | 12 px | help panel inner edge, palette, dialogs, tour popover |
| `--ss-radius-pill` | 999 px | status chips, level switch segments |
| `--ss-elev-0` | none | everything in the page flow |
| `--ss-elev-float` | dark: `0 12px 32px rgb(0 0 0 / .45)`; light: `0 12px 32px rgb(23 32 48 / .18)` | floating surfaces only |

No gradients. No shadows on in-flow cards (surface colour difference separates them).

### 2.5 Motion

| Token | Value | Use |
|---|---|---|
| `--ss-dur-press` | 90 ms | button press, toggle |
| `--ss-dur-small` | 160 ms | hover, disclosure, tooltip |
| `--ss-dur-panel` | 240 ms | help panel, palette, dialog, level switch content cross-fade |
| `--ss-dur-reveal` | 600 ms | split reveal when a separation completes (once) |
| `--ss-ease-enter` | `cubic-bezier(0.2, 0, 0, 1)` | entering |
| `--ss-ease-exit` | `cubic-bezier(0.3, 0, 1, 1)` | leaving |

Only `transform` and `opacity` are animated. Meters are never transitioned (the engine writes them
every frame). `html[data-motion="reduced"]` (preference or `prefers-reduced-motion: reduce`) sets
all durations to 0 ms except opacity fades capped at 120 ms; the split is drawn in its final state.

### 2.6 Layers

`--ss-z-rail` 10, `--ss-z-transport` 20, `--ss-z-panel` 30 (help), `--ss-z-popover` 40
(tooltips), `--ss-z-dialog` 50 (Radzen dialogs, palette), `--ss-z-tour` 60.

### 2.7 Radzen variable map (`wwwroot/css/radzen-map.css`)

Base theme `software` (free, MIT). Every Radzen variable StemStudio relies on is mapped; T012
completes the list from Radzen 13.1.0 `_css-variables.scss` *(spike)* and records hard-coded
colours that need targeted overrides.

| Radzen variable (examples) | Mapped to |
|---|---|
| `--rz-primary`, `--rz-primary-dark(er)`, `--rz-primary-light(er)`, `--rz-on-primary` | `primary`, `on-primary` (shades via `color-mix` with `base`) |
| `--rz-secondary`, `--rz-info` | `muted`, `ink` |
| `--rz-success`, `--rz-warning`, `--rz-danger` (+ `-light`, `-dark`) | state tokens |
| `--rz-base-50` .. `--rz-base-900`, `--rz-base-background-color` | ramp from `raised` to `ink` |
| `--rz-text-color`, `--rz-text-secondary-color`, `--rz-text-tertiary-color` | `ink`, `muted`, `muted` |
| `--rz-border-radius` | `--ss-radius-control` |
| `--rz-text-font-family` | `--ss-font` |
| input / button / card / dialog / tooltip / progress-bar / badge / sidebar variables | corresponding tokens |
| focus outline variables | `--ss-color-focus`, 2 px, offset 2 px |

## 3. Component inventory

### 3.1 Existing components mapped to the new design

| Existing (`src/StemStudio.Web/Components/...`) | New design | Notes |
|---|---|---|
| `Layout/MainLayout.razor` (RadzenHeader + body) | **AppShell**: `NavRail` + `ContextBar` + content gate + `HelpPanel` slot + `CommandPalette` | skip link first; `nav-*` ids kept on rail links |
| `Layout/NavMenu.razor` | **NavRail** (icon + label: Import `download`, Tracks `library_music`, Mixes `tune`; Help at the bottom) | active item: 3 px ink bar + raised background |
| `Pages/Import.razor` | **Import**: title "Import a song", URL field + Import button (primary), drop zone for files, device line, legal notice, limitation note; WelcomePanel above at first run; JourneyRail (Beginner) | legal notice never hidden; limitation note is a quiet card with "Learn more" |
| `Pages/Tracks.razor` | **Library**: rows with StemRibbon, title, artist, status chip, duration, Open mixer; search + sort (Advanced+); compact table (Expert) | empty state: "No tracks yet" + Import button |
| `Pages/TrackDetail.razor` | **Track**: header (title display, artist, StemRibbon), processing card with **SplitProgress**, DeviceBadge, actions (Retry, Delete with inline confirm) | Expert: JobDetails disclosure |
| `Pages/TrackSummary.razor` | **Facts row** (tempo, duration, model as label-value pairs) + StemTabs + Open mixer (primary) | |
| `Pages/DetailSection.razor`, `DetailStemList.razor` | **Detail stems card**: status chip, SplitProgress (small), outcomes as rows per parent with StemTab colours, actions | limitation note always visible |
| `Shared/DeviceBadge.razor` | unchanged structure, restyled as a quiet label-value line with a `memory` / `developer_board` icon | text, not colour |
| `Shared/ConfirmDialog.razor` | **Dialog** (float radius, elevation, title + text + actions right-aligned, destructive button danger) | |
| `Mixer/Mixer.razor` | **Console** layout: header (title, mix name, unsaved chip, Save / Save as, presets), panel row (Loop, Metronome), strips region (horizontal scroll), Master pinned right, **TransportDock** at the bottom | |
| `Mixer/ChannelStrip.razor` | **Strip**: StemTab (colour tab with condensed name), meter well, vertical fader with cap, pan knob-slider, M / S (>= 24 px), expand toggle | Beginner: no pan, read-only "panned" marker when pan != 0; Expert: numeric volume/pan fields, dB text |
| `Mixer/Meter.razor` | **Meter**: sunken well, fill by `--meter-level` (transform), clip lamp from `data-clip`; Expert: dB text from `data-peak-db` | engine contract unchanged |
| `Mixer/MasterSection.razor` | **MasterStrip** (ink tab "Master", wider meter) | |
| `Mixer/StemGroup.razor`, `ExpandToggle.razor` | **StemGroup**: detail strips indented under a parent-coloured bracket; patterns per detail | |
| `Mixer/TransportBar.razor` | **TransportDock** (sticky bottom; play primary, pause, stop, seek, counter in `type-counter`) | `scroll-padding-bottom` so focus is never hidden (WCAG 2.4.11) |
| `Mixer/LoopControls.razor` | **Loop panel**: Set A / Set B, loop toggle; A/B typed fields (Advanced+); A-B range drawn on the seek bar | |
| `Mixer/MetronomePanel.razor` | **Click panel**: toggle, volume; BPM, offset, beats per bar, reset (Advanced+) | |
| `Mixer/SaveMixDialog.razor` | **Dialog** (name field, error inline) | |
| `Pages/Mixes.razor` | **Mix library**: rows (mix name, track with StemRibbon, last modified, actions Load (primary), Duplicate, Rename, Delete inline at every level; icon-only with visible tooltip and accessible name at Expert) | action ids unchanged and always rendered |

### 3.2 New components

| Component | Purpose |
|---|---|
| `Design/StemTab`, `Design/StemRibbon`, `Design/StemColor` (helper) | stem identity |
| `Design/SplitProgress` | signature progress visual (decorative SVG, `aria-hidden`) |
| `Design/EmptyState`, `Design/ErrorState`, `Design/Skeleton` | designed states |
| `Experience/LevelGate`, `Experience/ExperienceContext`, `Experience/LevelSwitch` | levels |
| `Experience/WelcomePanel`, `Experience/JourneyRail` | first run, Beginner journey |
| `Mixer/PracticePresets` | presets |
| `Help/HelpPanel`, `Help/HelpHint`, `Help/HelpTopicView`, `Help/HelpSearch` | help |
| `Help/TourOffer` | tour offer chip |
| `Shell/CommandPalette`, `Shell/ShortcutOverlay`, `Shell/PreferencesMenu` | expert speed, preferences |
| `Pages/JobDetails` | Expert technical details |

## 4. Screens and wireframes per level

Legend: `[Button]` primary filled, `(Button)` secondary, `{field}`, `~~~` stem ribbon, `?` help hint.

### 4.1 App shell (all screens)

```
+------+---------------------------------------------------------------------+
| logo | Page title           [journey rail - Beginner]   Beginner|Adv|Exp  Cmd-K ? Prefs|
|------+---------------------------------------------------------------------|
| Imp  |                                                                     |
| Trk  |   content (max 1280, left aligned)                 | help panel    |
| Mix  |                                                                     | (400 px, lg: |
|      |                                                                     |  docked)     |
| Help |                                                                     |              |
+------+---------------------------------------------------------------------+
```

Under 768 px: rail becomes a bottom bar (Import, Tracks, Mixes, Help); the level switch moves into
the preferences menu; help panel is a full-screen sheet.

### 4.2 Import

- **Beginner**: WelcomePanel (first run) -> journey rail with step "Import" current -> title ->
  one large URL field with [Import] -> drop zone "Or drop an MP3, WAV or FLAC file (max 200 MiB)" ->
  device line -> legal notice -> limitation note (short version + "Learn more") -> TourOffer.
- **Advanced**: same without journey rail and tour offer (unless never seen); hints on focus only.
- **Expert**: same, compact; URL field focused on load; `kbd` hints ("Enter to import").

```
 Import a song
 {https://youtu.be/...                                   } [Import]
 +-------------------------------------------------------------+
 |  Drop an MP3, WAV or FLAC file here, or (Choose a file)      |
 +-------------------------------------------------------------+
 Processing device: CPU                      ?
 Imports are for personal practice only...   (legal notice, always visible)
 What StemStudio can separate ... (Learn more)
```

WelcomePanel (first run, all widths, non-modal, above the form):

```
 Welcome to StemStudio. How do you want to start?
 ( I am new to this )  ( I know my way around )  ( Give me everything )
   guided steps, presets    all controls, as usual    shortcuts, numbers, details
 (Take a one-minute tour)                                    (Not now)
```

### 4.3 Tracks (library)

```
 Tracks                                        {Search tracks} (Sort: newest)   <- Advanced+
 ~~~~~~  Hero of the Day    Metallica     (Completed)   4:22   (Open mixer)
 ~~~~~~  Fixture song       -             (Separating 42 %)  0:30
```

Ribbon: six thin coloured lines (grey for missing stems, a single grey line while separating).
Beginner: rows are large cards with the primary action "Practise" (= open mixer, keeps
`track-mixer-link`); Expert: dense table, columns BPM and model added.

### 4.4 Track detail

```
 Hero of the Day                        ~~~~~~ (ribbon)
 Metallica
 +-- Processing ------------------------------------------------+
 | (Separating)  Separating stems                               |
 | ─────────────┬═══ vocals                                      |  SplitProgress (decorative)
 |              ├═══ drums ...                    42 %          |  + progress bar (accessible)
 | Processed on: GPU (NVIDIA ...)                               |
 | (Retry)  (Delete track)                                      |
 +--------------------------------------------------------------+
 Tempo 117 BPM   Duration 4:22   Model htdemucs_6s
 [vocals][drums][bass][guitar][piano][other]        [Open mixer]
 +-- Detail stems ----------------------------------------------+
 | (Completed)  drums: split into kick, snare, ...               |
 | ...  limitation note (always visible)                         |
 +--------------------------------------------------------------+
 Expert: v Job details (model, stage, device, fallback reason, started/finished, durations)
```

Beginner: journey rail step "Separate" current; when completed the [Open mixer] button is the
primary action and the rail moves to "Mix".

### 4.5 Mixer

Advanced (= today's controls, new layout):

```
 Hero of the Day - Mix "Practice A"  (Unsaved changes)   (Presets v) (Save as) [Save]
 +- Loop --------------------+ +- Click -------------------------------------+
 | (Set A) (Set B) {A s}{B s}| | (Click on) vol ──○── {BPM} {offset} {beats} |
 | (Loop off)                | | (Reset)                                     |
 +---------------------------+ +---------------------------------------------+
 +-------+-------+-------+-------+-------+-------+   +--------+
 |vocals |drums  |bass   |guitar |piano  |other  |   | Master |
 | ▮     | ▮     | ▮     | ▮     | ▮     | ▮     |   |  ▮▮    |
 | ║○    | ║○    | ║○    | ║○    | ║○    | ║○    |   |  ║○    |
 | pan   | pan   | pan   | pan   | pan   | pan   |   |        |
 | M  S  | M  S  | M  S  | M  S  | M  S  | M  S  |   |        |
 |       |(Expand)|      |       |       |       |   |        |
 +-------+-------+-------+-------+-------+-------+   +--------+
 [Play] (Pause) (Stop) ──────[A=====B]──────○────────   1:02.4 / 4:22
```

Beginner: journey rail step "Mix"; preset row shown as chips above the strips ("Play along:
drums", "Karaoke", ...); loop panel only Set A / Set B / Loop; click panel only on/off and volume;
no pan (read-only "panned" marker when pan != 0); inline hints visible under the panels.

Expert: compact density; presets in a menu; under every fader numeric volume (0-100) and pan
(-100..100) fields; dB text under each meter; `kbd` hints on transport and M/S; a channel can be
selected (outline) for keyboard M/S.

### 4.6 Mix library

```
 Mix library                                               {Search mixes} <- Advanced+
 Practice A    Hero of the Day ~~~~~~   15 Nov 2026 14:02   [Load] (Duplicate) (Rename) (Delete)
```

Expert: compact rows, icon buttons. Empty: "No saved mixes yet. Open a track in the mixer and press Save."

### 4.7 Help panel

```
 +-- Help ------------------------------- (x) +
 | {Search help}                               |
 | < Back                                      |
 | Pan                                         |
 | Moves a stem between the left and right ... |
 | Available at Advanced level  (Switch)       |  <- only when gated above the current level
 | Related: Volume, Solo, Mute                 |
 | ------------------------------------------- |
 | Topics: Import, Tracks, Mixer, Glossary      |
 | Tours: Mixer basics (Start)                  |
 | Keyboard shortcuts                           |
 +---------------------------------------------+
```

### 4.8 Command palette and shortcut overlay

Palette: dialog at top centre, 640 px, input + grouped results (Go to, Tracks, Mixes, Actions,
Help), arrow keys + Enter, Escape returns focus. Overlay: two-column table of the current screen's
shortcuts, grouped (Transport, Loop, Channels, Help).

### 4.9 Tour popover

Float radius, raised surface, step counter "2 of 5" (a real sequence), title, text, (Back) [Next] /
[Done], close. Highlight cut-out with `scrim` around the target.

### 4.10 States

Every data screen has: loading (Skeleton in the shape of the content, `role="status"` text kept),
empty (EmptyState: one sentence + the action), error (ErrorState: what happened + how to fix +
retry where possible; existing error ids remain on the text element).

## 5. Feature catalogue (levels)

Binding source for FR-013/FR-014; implemented in `src/StemStudio.Web/Experience/FeatureCatalog.cs`.
`Min` = lowest level that shows it; `Max` = highest level that shows it (aids).

| Feature id | Controls (test ids) | Min | Max | Help topic |
|---|---|---|---|---|
| `mixer.pan` | `pan-<stem>`, `pan-<parent>-<detail>` | Advanced | - | `mixer-pan` |
| `mixer.pan-marker` | `pan-marker-<key>` (read-only, only when pan != 0) | Beginner | Beginner | `mixer-pan` |
| `mixer.loop.typed` | `loop-start`, `loop-end` | Advanced | - | `mixer-loop` |
| `mixer.metronome.advanced` | `metronome-bpm`, `metronome-offset`, `metronome-beats-per-bar`, `metronome-reset` | Advanced | - | `mixer-metronome` |
| `mixer.numeric` | `fader-value-<key>`, `pan-value-<key>` | Expert | - | `mixer-numeric-entry` |
| `mixer.meter.db` | dB text on meters (`meter-db-<key>`) | Expert | - | `mixer-meters` |
| `mixer.presets` | `mixer-presets`, `preset-<id>` | Beginner | - | `mixer-presets` |
| `tracks.search` | `tracks-search`, `tracks-sort` | Advanced | - | `tracks-library` |
| `mixes.search` | `mixes-search` | Advanced | - | `mixes-library` |
| `track.job-details` | `job-details` | Expert | - | `track-job-details` |
| `shell.shortcut-overlay-key` | Ctrl+/ opens `shortcut-overlay` | Expert | - | `shortcuts` |
| `shell.kbd-hints` | `kbd` hints on buttons | Expert | - | `shortcuts` |
| `journey.rail` | `journey-rail` | Beginner | Beginner | `journey` |
| `help.inline-hints` | visible hint text under panels (`hint-<topic>`) | Beginner | Beginner | per topic |
| `help.tour-offers` | `tour-offer-<tourId>` (for tours targeting the level) | Beginner | Expert | `tours` |

Everything not listed is shown at every level. Density default: comfortable (Beginner, Advanced),
compact (Expert); the user can override density in preferences.

## 6. Keyboard map

Active outside text fields, number fields, selects, dialogs and the tour (same guard as
`mixer-keys.js`). `Mod` = Ctrl (Cmd on macOS).

| Keys | Action | Scope |
|---|---|---|
| Space | play / pause (existing, unchanged) | mixer |
| `Mod+K` | command palette | global |
| `?` or F1 | help panel for the current context | global |
| `Mod+/` | shortcut overlay (Expert; other levels: from the help panel) | global |
| `Esc` | close the top-most panel / palette / tour | global |
| `G` then `I` / `T` / `M` | go to Import / Tracks / Mixes | global |
| `Home` | stop and return to start | mixer |
| `←` / `→` | seek -5 s / +5 s (`Shift`: 1 s) | mixer, when no slider has focus |
| `[` / `]` | set loop A / B at the current position | mixer |
| `L` | loop on/off | mixer |
| `K` | metronome on/off | mixer |
| `1`..`9` | select channel n (standard strips in order, master excluded) | mixer |
| `M` / `S` | mute / solo the selected channel | mixer |
| `Mod+S` | save mix; `Mod+Shift+S` save as | mixer |

Every shortcut has a visible control; shortcuts are listed in the overlay and the help topic
`shortcuts`; they never use single keys while a modifier other than Shift is held, except the
`Mod` combinations above.

## 7. Help content inventory (minimum for 004)

Areas and topics (ids are kebab-case; file `Help/topics/<area>/<id>.md`):

| Area | Topics |
|---|---|
| `start` | `welcome`, `levels`, `journey`, `tours`, `preferences`, `shortcuts`, `legal-notice` |
| `import` | `import-screen` (screen topic), `import-url`, `import-file`, `processing-device`, `stem-limitations` |
| `tracks` | `tracks-library` (screen), `track-status`, `track-screen` (screen), `track-progress`, `track-retry`, `track-delete`, `track-facts`, `track-job-details` |
| `detail` | `detail-stems`, `detail-outcomes`, `detail-limitations`, `detail-cancel-retry` |
| `mixer` | `mixer-screen` (screen), `mixer-volume`, `mixer-pan`, `mixer-mute-solo`, `mixer-meters`, `mixer-master`, `mixer-transport`, `mixer-loop`, `mixer-metronome`, `mixer-presets`, `mixer-detail-groups`, `mixer-save`, `mixer-numeric-entry` |
| `mixes` | `mixes-library` (screen), `mixes-actions` |
| `glossary` | `glossary` (index), `stem`, `detail-stem`, `bpm`, `beat-grid`, `downbeat`, `time-signature`, `pan`, `solo`, `mute`, `loop`, `metronome-offset`, `peak-meter`, `clipping`, `dbfs`, `gpu-cpu` |

Tours (`Help/tours/<id>.json`):

| Tour | Route | Levels | Steps (markers) |
|---|---|---|---|
| `import-basics` v1 | `/` | Beginner, Advanced | `import-url`, `import-file`, `processing-device`, `legal-notice`, `help-button` |
| `track-basics` v1 | `/tracks/{id}` | Beginner, Advanced | `track-progress`, `track-facts`, `detail-stems`, `open-mixer` |
| `mixer-basics` v1 | `/tracks/{id}/mixer` | Beginner | `mixer-presets`, `mixer-volume`, `mixer-mute-solo`, `mixer-transport`, `mixer-loop`, `mixer-save` |
| `mixer-advanced` v1 | `/tracks/{id}/mixer` | Advanced | `mixer-pan`, `mixer-detail-groups`, `mixer-loop`, `mixer-metronome`, `mixer-save` |
| `expert-keyboard` v1 | any | Expert | `command-palette`, `shortcut-overlay`, `mixer-numeric-entry` |

## 8. Voice and copy

- Plain verbs, active voice, sentence case, no exclamation marks, no apologies in errors.
- An action keeps its name through the flow: "Save" -> toast "Saved"; "Import" -> "Importing ..."
  -> "Imported".
- Empty states invite the next action ("No tracks yet. Import a song to get started.").
- Errors say what happened and what to do; existing error texts from 001-003 are kept verbatim
  where tests compare them.
- Level names in the UI: "Beginner", "Advanced", "Expert"; welcome choices: "I am new to this",
  "I know my way around", "Give me everything".
- Preset names: "Play along: drums", "Play along: bass", "Play along: guitar", "Play along: piano",
  "Karaoke" (vocals muted), "Focus: vocals" (vocals solo), "Reset mix".

## 9. Accessibility rules (WCAG 2.2 AA)

- Landmarks: `nav` (rail), `header` (context bar), `main`, `aside` (help panel, `aria-label="Help"`);
  one `h1` per screen; `FocusOnNavigate` keeps targeting `h1`.
- Focus ring from `--ss-color-focus`, always visible on keyboard focus (`:focus-visible`), never
  removed; sticky transport and help panel never cover the focused element (2.4.11:
  `scroll-padding-bottom` = transport height; docked panel reflows content).
- Targets >= 24 x 24 px including M/S and expand (2.5.8); faders and pan keep keyboard arrows and
  the Expert numeric fields as non-drag alternatives (2.5.7); A/B loop via buttons.
- Help in the same place on every screen (3.2.6): top-bar help button and rail Help item.
- Status and progress announced with `role="status"` / progressbar values; the split visual is
  `aria-hidden="true"`.
- Level switch: radio group semantics (`role="radiogroup"`, arrow keys), announces "Level set to
  Advanced".
- Colour is never the only carrier: stem names on tabs, status chips with text, meter clip has
  `data-clip` plus a visible "Clip" label at Expert and an accessible description.
- Forced colours: tokens fall back to system colours (`@media (forced-colors: active)`), tabs keep
  a border.
- Reflow at 320 CSS px and 200 % text zoom for all screens except the mixer strip region, which
  scrolls within itself (1.4.10 exception for two-dimensional layouts).
