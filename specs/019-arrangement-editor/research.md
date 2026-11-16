# Research: Arrangement Editor (feature 019)

Architect, 2026-11-16. Each item: decision, rationale, alternatives. Binding summary in ADR-028.
Items marked **(verify in wave)** are checked by the named owner at the start of the 019 wave,
when 012-018 are merged; a negative result triggers the fallback named in the item, not a
redesign.

## R-01 Timeline model: edit list of source regions (ADR-028 D1, D2)

- **Decision**: ordered regions over source time (`[start, end)` seconds, repeat, mutes),
  canonical seconds, bar snapping as a validation rule; own entity, referenced by mixes.
- **Rationale**: non-destructive, tiny (64 regions x ~120 bytes), independent of grid
  re-analysis; all followers (lyrics, score, sections) are keyed to source time already.
- **Alternatives**: (a) bar indices as storage: break silently when the grid is re-analysed;
  (b) arranged-time clips (DAW model, each clip with its own arranged position and gaps):
  allows gaps/overlaps that a practice tool does not need and complicates "chorus 4x"; (c) edit
  list inside the mix: one arrangement could not serve two mixes and the mix contract would lose
  its flat shape; (d) rendering arranged stems: destructive and up to 1.6 GB per version.

## R-02 Where bars come from

- **Decision**: bar lines = `0`, every `BeatGrid.downbeats` value `< duration`, and `duration`,
  deduplicated within 1 ms. Bar 0 is the pickup when the first downbeat is later than 1 ms.
  014 sections are mapped to bars by their start/end times (they are snapped to bars by 014; if a
  section edge is not on a bar line the seed snaps it to the nearest line and notes it).
- **Rationale**: no new analysis (beat-analysis has no work); the same grid drives the metronome
  so bar-snapped regions keep the accent pattern.
- **Alternatives**: beat-level snapping (too fine for "chorus 4x", breaks accents when bars are
  odd lengths; Expert free timing covers special cases); a separate bar model in the worker
  (duplicate of 001's grid).

## R-03 Click-free boundaries: crossfade law (ADR-028 D4)

- **Decision**: half-overlap equal-power crossfade centred on the boundary, `F` even frames
  (default 10 ms = 442 frames), per channel **runs** (contiguous open segments are one read).
- **Rationale**: a 10 ms equal-power crossfade is the common click-suppression length for
  musical edits (long enough to avoid a broadband click, short enough not to smear transients);
  centring keeps bar lines and the arranged length exact; runs make contiguous boundaries
  bit-transparent, so "Split" never changes the sound (US2 AS2, SC-004 song-order identity).
- **Alternatives**: linear crossfade (-3 dB dip on uncorrelated material); fade-out then fade-in
  to silence (audible duck); zero-crossing search (per channel different cut points, breaks the
  sample lock between channels; useless on 22 correlated stems); longer default fades (smear
  drum hits at the boundary).
- **Correlated repeats**: a chorus followed by itself is partly correlated; equal-power can
  give up to +3 dB for 10 ms on fully correlated material. Inaudible at 10 ms and covered by the
  007 clip protection in exports.

## R-04 Engine scheduling and the 012 seam (ADR-028 D5) **(verify in wave: audio-engine)**

- **Decision**: one `AudioBufferSourceNode` per run with `start(when, offset, duration)` and a
  `GainNode` fade curve (`setValueCurveAtTime`, `F` points, i.e. one point per frame), scheduled
  up to 2 s ahead by the existing look-ahead scheduler, all times from the single anchor.
- **Rationale**: `start()` with a context time is sample-accurate and identical for all channels
  of a boundary, so the lock of ADR-005 holds; buffers are shared (no copies); at most about two
  sources per channel are alive (current run + next run during a crossfade).
- **Memory check**: concatenating arranged buffers would need
  `1800 s x 44100 x 2 ch x 4 B = 635 MB` per channel, about 14 GB for 22 channels - rejected.
- **012 seam**: 012 (engine 2.3.0, SoundTouch WASM, LGPL) changes speed and pitch. Two possible
  shapes: (1) sources play at `playbackRate = r` and a SoundTouch worklet corrects the pitch after
  the source: then run times in context seconds are arranged seconds divided by `r` and nothing
  else changes; (2) a worklet reads PCM itself: then the arrangement provides
  `TimelineMap.read(channel, arrangedFrame, count)` returning gain-weighted source frames (the
  same run law, same vectors) and the worklet calls it instead of reading linearly. The
  audio-engine owner confirms which shape 012 shipped in task T030 and implements only that one.
- **Alternatives**: AudioWorklet arrangement reader as the primary design (needs
  SharedArrayBuffer and COOP/COEP headers or a copy of every stem into the worklet; kept as the
  fallback seam only); scheduling every run at play time (256 segments x 22 channels = 5,632 nodes).

## R-05 Followers: lyrics, score, sections, metronome (ADR-028 D6)

- **Decision**: the engine's existing position API stays **source time**; new timeline position
  API is additive. The karaoke clock reads `positionAt(time)`, which returns source time, so
  karaoke needs no change; the 014 score cursor and section highlight consume
  `OnPositionChanged` (source seconds) unchanged. The metronome builds clicks per segment from
  the source grid (generalising today's loop segments in `metronome/timeline.ts`).
- **Rationale**: zero change in 006/014 code; the user sees the material that is heard; repeated
  sections show the same lyrics/score again, which is what a practising musician expects.
- **Alternatives**: unrolled arranged score/lyrics (needs re-engraving and a second lyrics
  timeline per arrangement; candidate for 020 handouts); hiding lyrics in vocal-muted regions
  (wrong for singers practising).

## R-06 Resolve on the server, live editing over HTTP

- **Decision**: `POST /api/tracks/{trackId}/arrangements/resolve` (stateless) validates an
  unsaved document and returns the resolved timeline; the editor debounces 150 ms.
- **Rationale**: one resolver (C#, Domain) used by the editor, save, mix load and export; the
  Blazor Server UI must not reference the Domain directly (layering UI -> API client -> API ->
  domain). A localhost round trip is a few milliseconds, inside SC-005 (200 ms).
- **Alternatives**: a TypeScript resolver (second implementation); Domain referenced from Web
  (breaks the dependency rule enforced by quality-gate).

## R-07 Export through 007 (ADR-028 D7)

- **Decision**: the worker request gains `timeline` (flat segments with `mutedInputs` indices,
  `crossfadeFrames`); the worker renders arranged frames block-wise with the run law; ranges are
  in arranged time; the recipe stores the resolved timeline.
- **Estimate**: 007 measured about 9 s for six 4:22 stems to MP3 320 (about 2 s per minute of
  output, MP3 encode dominant). A 10-minute arranged version is about 20 s plus seeks
  (`soundfile.seek` on FLAC is a frame-accurate seek; 256 segments x 6 inputs = 1,536 seeks, each
  well under 10 ms) -> SC-006 30 s. **(verify in wave: audio-ingest, T040)**
- **Alternatives**: rendering in the browser (agent and retry cannot use it, ADR-016 D2);
  pre-concatenating the inputs in the API (file I/O of up to 1.6 GB per export).

## R-08 Undo/redo and saving (ADR-028 D8)

- **Decision**: immutable document snapshots in the editor (C# records), 100-step stack,
  explicit save with optimistic concurrency (`revision`), unsaved-changes guard.
- **Rationale**: documents are tiny (64 regions), so snapshots are simpler and safer than
  inverse commands; explicit save matches the mixer's "Save mix" model.
- **Alternatives**: server-side history (not needed for one local user); autosave (surprising
  with live playback edits; YAGNI).

## R-09 Keyboard model and accessibility

- **Decision**: region lane = `role="listbox"` (`aria-orientation="horizontal"`), regions =
  `role="option"` with a full `aria-label`; song strip = a bar list with roving focus and
  Shift-extend selection plus "From bar"/"To bar" number fields; a polite live region announces
  every change; shortcuts are bound on the lane/strip only (no global single-key shortcuts);
  every drag has a button or field alternative. Keys: Left/Right/Home/End focus; Alt+Left/Right
  move; + / - repeat; Ctrl+D duplicate; Delete/Backspace remove; S split at playhead bar; M mutes;
  Enter play from region; L loop region; Ctrl+Z undo; Ctrl+Shift+Z / Ctrl+Y redo; F1 help.
- **Rationale**: WAI-ARIA APG listbox pattern is the closest standard for an ordered, editable
  sequence; WCAG 2.1.1, 2.1.4, 2.4.7, 2.5.7, 4.1.3.
- **Alternatives**: `role="grid"` (heavier and confusing for a single row); drag only (fails
  2.5.7).
- **Conflict check** with 004 expert keyboard and 014 hands-free control: those use global keys
  (Space play/pause etc.); the arrangement keys are only active with focus inside the timeline,
  and Space keeps its global meaning there.

## R-10 Levels and help

- **Decision**: feature-catalogue ids `mixer.arrange` (Advanced), `mixer.arrange.crossfade`,
  `mixer.arrange.free-timing` (Expert); help area `mixer` (no help-content version bump), topics
  listed in contracts/DELTA.md 6.
- **Rationale**: C6 is a "Could" with editing complexity; Beginners still benefit from
  arrangements their teacher or their Advanced self made (read-only chip).

## R-11 Interplay with 015, 017, 018, 011

- 015 recording: disabled while arranged (aligning takes to arranged time needs a 015 contract
  change; backlog).
- 017 effects: region mutes act before the strip, so effects/meters are unchanged; reverb tails
  ring out after a mute (natural).
- 018 remote: commands unchanged; the remote shows song time (remote-protocol unchanged).
- 011 agent: later catalogue minor; the API has closed schemas and readable problems now.

## R-12 Browser verification of the law **(verify in wave: qa)**

- **Decision**: `e2e/tests/timeline-law.spec.ts` (no stack) loads the built engine with
  `EngineOptions.audioContextFactory` returning an `OfflineAudioContext` and `fetch` serving the
  vector signals as generated WAV, sets the `timeline.samples` plans and compares the rendered
  frames with the vectors at 1e-4 (float32 + curve interpolation).
- **Fallback** if the offline render cannot be driven through the public engine API: an
  engine-exported test helper `renderTimelineOffline` (test-only re-export, no contract change).
