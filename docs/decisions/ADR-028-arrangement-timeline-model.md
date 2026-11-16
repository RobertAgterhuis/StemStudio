# ADR-028: Arrangement timeline model (feature 019)

- **Status**: Proposed (architect, 2026-11-16, 019 planning round). Becomes Accepted in 019
  Phase 0 (tasks.md T006) when the contracts are merged onto the versions of 012-018.
- **Number**: chosen as ADR-021 + (019 - 012), so the parallel 012-020 planning rounds do not
  collide; the CTO renumbers at the merge if another allocation was used.
- **Context documents**: `specs/019-arrangement-editor/{spec.md, research.md, data-model.md,
  contracts/DELTA.md}`, ADR-005 (engine, sample lock, look-ahead scheduling), ADR-010 (gate rule
  v2), ADR-015 (karaoke clock), ADR-016 (asset library, worker rendering, shared vectors),
  `docs/MoSCoW.md` C6, `docs/ROADMAP.md` (019 depends on 007 and 014; contracts backend 1.18.0,
  worker 1.10.0, engine 2.8.0, UI test ids 1.18.0, render-vectors 1.2.0).

## Context

The owner wants to "cut, repeat and rearrange sections of stems into a new practice version
(e.g. chorus 4x)" and export it (C6). The practice version must:

1. never change the stems (non-destructive) and be editable later, with undo/redo;
2. play gapless and sample-locked in the browser engine, with no clicks at cut points;
3. drop channels per part ("drop the guitar in verse 2") for every instrument family equally;
4. keep lyrics (006), score follow (014), section highlight (014), metronome and count-in (012)
   meaningful;
5. sound identical when exported through the 007 render pipeline (agent and retry without a
   browser, ADR-016 D2), and be verifiable with golden vectors (render-vectors 1.2.0);
6. survive later edits, beat-grid re-analysis and detail-phase re-runs without silent drift.

## Decisions

### D1 An arrangement is an edit list of source regions, stored as its own entity

An **Arrangement** belongs to one track and holds an ordered list of 1-64 **regions**. A region
is a half-open span of the *source* (the song as separated) `[startSeconds, endSeconds)`, a
`repeat` count 1-16, a set of per-region **channel mutes**, an optional label and the id of the
014 section it came from. Arrangement-wide settings: `snap` (`bars` | `free`) and `crossfadeMs`
(0-50, default 10). It is stored in its own table (`Arrangements`, cascade on track delete),
with an optimistic-concurrency `revision`. A **Mix may reference one arrangement**
(`arrangementId`, nullable, set to null when the arrangement is deleted); loading such a mix
loads the arrangement. "Song order" (no arrangement) stays the default everywhere.

Rejected: storing the edit list inside the mix (one arrangement could not serve several mixes;
the mix PUT would grow by up to 64 regions and lose its small, flat shape); rendering a new
"arranged track" with new stems (destructive, duplicates up to 1.6 GB per version, and breaks
lyrics/score/section data that are keyed to source time).

### D2 Canonical times are source seconds; bars are a snapping rule, not the storage

Region edges are stored as source seconds. With `snap = bars` (default when the track has a
beat grid with downbeats) every edge must lie on a **bar line** (`0`, a downbeat, or the track
end; tolerance 1 ms) and is stored as that exact line value; the UI edits bars and shows bar
numbers (bar 0 = pickup before the first downbeat). With `snap = free` (Expert) edges are any
time; regions are at least 0.5 s. Bars are derived, not stored, so a re-analysed beat grid never
silently moves audio: the arrangement keeps sounding the same and is flagged `stale` with a
warning when its edges are no longer on bar lines.

### D3 The backend resolves; one flat "resolved timeline" feeds the engine and the worker

`TimelineResolver` (C#, Domain) validates the document against the track (duration, bar lines,
channel ids, limits) and expands it into a flat list of **segments** (repeats laid end to end):
`{regionIndex, repeatIndex, arrangedStartFrame, sourceStartFrame, frameCount, mutes, bars}` at
44.1 kHz, with `frame(x) = floor(x * 44100 + 0.5)` and an even crossfade length
`F = 2 * floor(ms * 44.1 / 2 + 0.5)` frames. Every API response that carries an arrangement
carries its resolved timeline, and a stateless `resolve` endpoint serves unsaved editor states,
so neither the browser nor the worker re-implements validation or bar snapping.

Rejected: resolving in TypeScript (the agent and export retry have no browser; two resolvers
would drift); sending regions with repeats to the worker (it would need bar and duration logic).

### D4 Playback law: per-channel runs with half-overlap equal-power crossfades

For each channel, consecutive segments in which the channel is open (neither it nor its parent
is muted) and whose inner boundaries are **contiguous in the source** form one **run**: one
continuous read of the stem. A run that does not start the timeline begins `F/2` frames early
with a fade-in; a run that does not end the timeline ends `F/2` frames late with a fade-out.
Fade gains over `k = 0..F-1` are `sin(pi/2 (k+0.5)/F)` (in) and `cos(pi/2 (k+0.5)/F)` (out),
so the crossfade is centred on the boundary, keeps every bar line and the arranged length
exactly where the edit list puts them, and has equal power (`sin^2 + cos^2 = 1`). Source frames
outside the file are silence. Consequences:

- a contiguous boundary without a mute change is **bit-transparent** (no fade, one source);
- a mute change at a contiguous boundary is a fade-out/fade-in of only that channel, reading the
  real neighbouring audio (no click, no gap);
- a jump (cut, repeat, reorder) crossfades every open channel; `F = 0` is a hard cut (Expert).

Rejected: fades inside each segment (shortens audible material and shifts the perceived bar
line by `F/2`); a fixed 5 ms fade-to-silence on every boundary (audible "duck" on every repeat);
linear crossfades (-3 dB dip on uncorrelated material).

### D5 Engine: a look-ahead run scheduler on the shared anchor, clocks map to source time

Engine 2.8.0 adds `setTimeline(plan | null)`. With a plan, the transport schedules each run as
one `AudioBufferSourceNode.start(when, sourceOffset, duration)` plus a per-run `GainNode` whose
fades are `setValueCurveAtTime` curves of `F` points, all computed from the single transport
anchor (`when`, arranged offset) and the existing look-ahead scheduler (wake-up only; times
come from `AudioContext.currentTime`, constitution V). Runs start at most 2 s ahead, so a
256-segment timeline never holds more than about two sources per channel. Region mutes act
*before* the channel strip (fader, gate, 012/017 processing), so the mixer, meters and effects
are unchanged. The engine's position clock stays **source time** for every existing consumer:
`getPosition()`, `OnPositionChanged`, the karaoke clock and the metronome read the source time
heard now (switching at the boundary centre); new `getTimelinePosition()`,
`OnTimelinePositionChanged(arrangedSeconds, segmentIndex)`, `seekTimeline`,
`setTimelineLoop` and `getTimelineDuration()` serve the editor. The metronome maps each source
beat inside a segment to arranged time, so accents stay on beat 1 when edges are on bar lines.
With 012's speed factor `r`, context durations are arranged durations divided by `r` at the
same seam where 012 converts track time today (research.md R-04).

Rejected: concatenating arranged `AudioBuffer`s in JS (a 30-minute arrangement of 22 channels is
about 14 GB of float32); a custom AudioWorklet reader (needs SharedArrayBuffer and cross-origin
isolation, or a copy of every stem into the worklet; kept as the fallback seam if 012 already
reads PCM in a worklet, research.md R-04).

### D6 Lyrics, score follow and sections follow the material, not an unrolled score

Because every follower reads source time, the karaoke ball, the 014 score cursor and section
highlight show what is *heard*: when the chorus repeats, the lyrics and the score jump back to
the chorus. Muting vocals in a region keeps the lyrics on screen (that is the point of a
sing-along practice version). An "unrolled" arranged score or lyric sheet is out of scope
(it would need re-engraving per arrangement; candidate for 020 handouts).

### D7 Export goes through 007 with a flat segment list; vectors pin the law in four places

`CreateAudioExportRequest` gains `arrangementId` or an inline `arrangement` (unsaved editor
state), mutually exclusive. The backend builds the 007 render plan as today and adds
`timeline: {crossfadeFrames, segments: [{sourceStartFrame, frameCount, mutedInputs}]}` to the
worker request (worker 1.10.0); a range is then in arranged time. The worker renders block-wise
over arranged frames with the D4 law (two passes, clip protection, encoders unchanged). The
asset recipe stores the resolved timeline snapshot, so retry reproduces the same file after the
arrangement is edited or deleted. `render-vectors.json` 1.2.0 (which contains 015's 1.1.0 unchanged; CTO allocation 2026-11-16) adds a `timeline` section
(resolve, spans, samples, clock, metronome) consumed by the C# resolver/planner, the TS engine,
the Python renderer and a browser `OfflineAudioContext` check.

### D8 Editing is client-side with an undo stack; saving is explicit

The editor works on an immutable document; every command (add, split, move, duplicate, remove,
repeat, mute, label, crossfade, snap) pushes one snapshot onto an undo stack of 100 steps (redo
cleared by a new command). Each change is resolved by the API (debounced 150 ms) and applied
to the engine live, also while playing (the arranged position is kept, clamped to the new
length). Saving is an explicit `PUT` with the expected `revision` (`409 arrangement-conflict`);
an unsaved-changes guard protects navigation. Undo history is per editor session, not stored.

### D9 Limits and levels

Limits: 64 regions, repeat 1-16, 256 segments, 30 minutes arranged, region >= 0.5 s, crossfade
0-50 ms, 50 arrangements per track, name 1-80 characters. The editor is an **Advanced** feature
(hidden at Beginner; a Beginner who opens a mix with an arrangement hears it and sees a
read-only "Arranged: <name>" chip with "Play song order"). Crossfade length, free timing and
seconds fields are **Expert**. Every action has a keyboard path (WCAG 2.1.1, 2.5.7).

### D10 What is deliberately not supported in 019

Recording (015) while an arrangement is active is disabled with a reason ("Switch to song
order to record"); takes stay aligned to source time. Time-stretching a single region, per-region
gain other than mute, fades longer than 50 ms, regions from other tracks, and an "unrolled"
score are out of scope (backlog). The 011 agent gets arrangement tools in a later tool-catalogue
minor (the API is agent-ready: closed schemas, readable problems).

## Consequences

- Positive: non-destructive, small storage (a few KB per arrangement), one law in four
  languages pinned by generated vectors, every existing follower keeps working unchanged,
  export reuses the whole 007 pipeline.
- Negative: the engine transport gains a second scheduling mode (song order stays the old path,
  byte-for-byte); the resolve round trip adds a few milliseconds per edit; the metronome in free
  timing can show irregular bars across cuts (documented in help).
- Risk: the 012 speed/transpose implementation may move PCM reading into a worklet; the D5
  fallback seam (TimelineMap read API, same vectors) covers it and is checked at the start of
  the 019 wave (research.md R-04).
