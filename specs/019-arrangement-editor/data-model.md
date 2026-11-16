# Data Model: Arrangement Editor (feature 019)

Binding for backend, frontend, audio-engine and audio-ingest. Law and constants are pinned by
`contracts/render-vectors.json` **1.2.0** (section `timeline`, generator
`contracts/generate_render_vectors.py`). Design: ADR-028. On any difference the vectors win,
then the OpenAPI documents, then this file.

## 1. Storage (backend, EF Core + SQLite)

### 1.1 Table `Arrangements` (migration `AddArrangements`)

| Column | Type | Rule |
|---|---|---|
| `Id` | GUID (PK) | server-generated |
| `TrackId` | GUID (FK -> `Tracks`, **cascade delete**) | indexed |
| `Name` | TEXT | trimmed, 1-80 characters, no control characters; unique per track, case-insensitive (`NameKey` = upper-invariant, unique index `(TrackId, NameKey)`) |
| `Revision` | INTEGER | 1 on create, +1 on every successful update (concurrency token) |
| `Snap` | TEXT | `bars` \| `free` |
| `CrossfadeMs` | INTEGER | 0-50, default 10 |
| `Regions` | TEXT (JSON) | `Region[]` (2.2), 1-64 entries |
| `CreatedAt`, `UpdatedAt` | TEXT (UTC ISO 8601) | `TimeProvider` |

At most 50 rows per track (`409 arrangement-limit`).

### 1.2 Table `Mixes` (existing): column `ArrangementId`

`ArrangementId` GUID NULL, FK -> `Arrangements` **ON DELETE SET NULL**. A mix may only
reference an arrangement of its own track (`422 mix.arrangementId: belongs to another track`).
Same migration `AddArrangements`. Existing rows: null (song order).

### 1.3 Assets (existing, 007)

No schema change: the arrangement snapshot lives in the JSON `Provenance.Recipe` (section 7).

## 2. Arrangement document (Domain `StemStudio.Domain.Arrangements`)

### 2.1 `ArrangementDocument`

```csharp
public sealed record ArrangementDocument(
    SnapMode Snap,                       // Bars | Free
    int CrossfadeMs,                     // 0..50
    IReadOnlyList<Region> Regions);      // 1..64
```

### 2.2 `Region`

```csharp
public sealed record Region(
    double StartSeconds,                 // source time, >= 0
    double EndSeconds,                   // source time, <= duration (+1 ms), End - Start >= 0.5
    int Repeat,                          // 1..16
    IReadOnlyList<string> Mutes,         // ChannelId values, unique, 0..56
    string? Label,                       // 0..40 characters, trimmed; null/empty = default label
    Guid? SectionId);                    // 014 section the region was created from (informational)
```

Default label: section name when `SectionId` is set and still exists, else `"Bars a-b"` (bars
mode) or `"m:ss.s-m:ss.s"` (free mode).

`ChannelId` is the 007 pattern `^(vocals|drums|bass|guitar|piano|other)(\.[a-z][a-z-]{0,31})?$`.
A mute must name a standard stem of the track or a kept detail stem of a split parent at save
time; on read, unknown mutes are kept but ignored and reported as warning `unknown-channel`.

### 2.3 Validation (`TimelineResolver.Validate`), error keys

Problem `422` with `errors: { "<key>": ["<code>: <readable text>"] }` (RFC 9457 validation
problem, as 001). Codes and keys exactly as `render-vectors.json` `timeline.resolve`:

| Code | Key | Rule |
|---|---|---|
| `count` | `regions` | 1..64 regions |
| `too-many-segments` | `regions` | sum of `repeat` <= 256 |
| `too-long` | `regions` | total frames <= 1800 s x 44100 |
| `out-of-range` | `crossfadeMs` | 0..50 |
| `no-bars` | `snap` | `bars` needs at least one downbeat |
| `outside-track` | `regions[i].endSeconds` | `start >= 0` and `end <= duration + 0.001` |
| `too-short` | `regions[i].endSeconds` | `end - start >= 0.5` |
| `out-of-range` | `regions[i].repeat` | 1..16 |
| `not-on-bar-line` | `regions[i].startSeconds` / `.endSeconds` | bars mode: within 1 ms of a bar line |
| `duplicate` | `regions[i].mutes` | unique |
| `unknown-channel` | `regions[i].mutes` | channel exists on the track (save/resolve of an input) |

All errors are collected (not first-fail), document-level first, then per region.

### 2.4 Bar lines

`lines = [0] ∪ {d ∈ downbeats : d < duration} ∪ [duration]`, sorted, a value is dropped when it
is within 1 ms of the previous kept value. Bar number of line `j`: `j` when the first downbeat is
later than 1 ms (pickup bar 0 exists), else `j + 1`. A region `[lines[a], lines[b])` covers bars
`bar(a) .. bar(a) + (b - a) - 1`. In bars mode stored edges are replaced by the exact line value.

**Stale**: on read, an arrangement in bars mode whose stored edges are not all within 1 ms of the
current lines is `stale = true` with warning `bar-lines-changed`; it resolves from its stored
seconds (sounds the same). "Snap to new bar lines" (UI command) moves each edge to the nearest
line and is a normal, undoable edit.

## 3. Timeline law (shared; ADR-028 D4)

Constants: `RATE = 44100`; `frame(x) = floor(x * RATE + 0.5)`;
`F = 2 * floor(crossfadeMs * RATE / 2000 + 0.5)` (10 ms -> 442, 50 ms -> 2206, 0 -> 0);
`h = F / 2`.

### 3.1 Segments

Regions in order; region `i` with `S = frame(start)`, `N = frame(end) - S` is emitted `repeat`
times as segments `{regionIndex: i, repeatIndex: r, arrangedStartFrame: A, sourceStartFrame: S,
frameCount: N, mutes, bars}`; `A` is the running sum of previous `N`. `totalFrames = Σ N`.

### 3.2 Openness and contiguity

- `open(seg, c)` ⇔ `c ∉ seg.mutes` and `parent(c) ∉ seg.mutes` (`parent("drums.snare") = "drums"`,
  `parent("drums") = "drums"`).
- `contiguous(prev, cur)` ⇔ `prev.sourceStartFrame + prev.frameCount == cur.sourceStartFrame`.

### 3.3 Runs of a channel

Scan segments in order; an open segment `k` extends the current run when the run ends at `k-1`
and `contiguous(seg[k-1], seg[k])`, else starts a new run. For a run over segments `i..j`:

- `fadeIn = F > 0 and i > 0`; `fadeOut = F > 0 and j < n - 1`;
- `lead = h if fadeIn else 0`; `tail = h if fadeOut else 0`;
- `arrangedStartFrame = A_i - lead`; `sourceStartFrame = S_i - lead`;
- `frameCount = A_j + N_j + tail - arrangedStartFrame`.

### 3.4 Gains and output

For a run frame at offset `o` (0-based from the run's arranged start):

- `o < F` and `fadeIn`: `g = sin(pi/2 * (o + 0.5) / F)`;
- `frameCount - o <= F` and `fadeOut`: `g = cos(pi/2 * (F - (frameCount - o) + 0.5) / F)`;
- else `g = 1`.

`y_c[t] = Σ_runs g · x_c[sourceStartFrame + o]` for `t = arrangedStartFrame + o` inside
`[0, totalFrames)`; `x_c[n] = 0` outside `[0, length_c)`. The channel output `y_c` then goes
through the unchanged 007 law (fader gain × gate, pan matrix, master; data-model 007 section 3).
Region min length (22,050 frames) is always > 2F (4,412), so fades of one run never overlap.

### 3.5 Clock

`source(t)` for arranged frame `t`: the segment `k` with `A_k <= t < A_k + N_k`;
`source = S_k + (t - A_k)`; at `t >= totalFrames` the timeline has ended. The switch happens
at the boundary itself (the centre of the crossfade). Seconds = frames / 44100.

### 3.6 Metronome

For each segment `k`, every source beat `b` with `S_k/RATE <= b < (S_k+N_k)/RATE` clicks at
arranged time `A_k/RATE + (b - S_k/RATE)`, accent when `b` is a downbeat (or per
`MetronomeSettings.beatsPerBar` counting as today). With `bpmOverride` the constant grid is laid
on the arranged timeline from arranged time 0 plus `offsetMs` (the source grid is not used).

### 3.7 Arranged loop and seeks (engine)

- `setTimelineLoop({start, end, enabled})` in arranged seconds, `0 <= start < end <=
  timelineDuration`. The loop wrap is a boundary: when the material at `end` and at `start` is not
  contiguous, a crossfade of `F` is applied exactly as between two segments (the scheduler treats
  the loop as the segment sequence `[start..end)` repeated).
- `seek(sourceSeconds)` while arranged: first segment occurrence `k` (searching from the current
  segment forward, wrapping) with `S_k <= x < S_k + N_k`; else the first segment (same search
  order) with `S_k > x`; else the timeline end (stopped). `seekTimeline(arrangedSeconds)` clamps to
  `0..duration`.

## 4. Resolved timeline (API, engine input)

```yaml
ResolvedTimeline:
  sampleRate: 44100
  crossfadeFrames: int            # F, even, 0..2206
  totalFrames: int                # <= 79,380,000
  durationSeconds: number         # totalFrames / 44100, rounded 1e-9
  segments: TimelineSegment[]     # 1..256
  warnings: string[]              # grid-unreliable, bar-lines-changed, unknown-channel:<id>
TimelineSegment:
  regionIndex, repeatIndex, arrangedStartFrame, sourceStartFrame, frameCount: int
  mutes: ChannelId[]              # effective mutes (unknown channels removed)
  bars: { first: int, last: int } | null   # null in free mode
```

Engine `TimelinePlan` (audio-engine 2.8.0) is the subset `{sampleRate, crossfadeFrames,
segments: [{sourceStartFrame, frameCount, mutes}]}`; `arrangedStartFrame` is recomputed by the
engine as a running sum and validated against nothing else (the API guarantees consistency).

## 5. Editor state (frontend, `StemStudio.Web/Arrange`)

- `ArrangeEditorState`: `ArrangementId?`, `Revision?`, `Name`, `Document` (immutable),
  `Resolved` (last `ResolvedTimeline` or the last validation problem), `IsDirty`, `Selection`
  (focused region index, selected bar range), `UndoStack`/`RedoStack` (`ImmutableStack` of
  documents, max 100; oldest dropped).
- Commands (each = one undo step): `AddRegion(barRange)`, `SplitAt(bar)`, `SetEdges(i, a, b)`,
  `Move(i, j)`, `Duplicate(i)`, `Remove(i)`, `SetRepeat(i, n)`, `ToggleMute(i, channel)`,
  `SetLabel(i, text)`, `SetCrossfade(ms)`, `SetSnap(mode)`, `SnapToBarLines()`,
  `ReplaceAll(document)` (new from sections/song). Label typing coalesces into one step per
  focus session. A command that would break a limit is refused locally (same codes as 2.3) before
  any HTTP call.
- Flow per command: document -> debounce 150 ms -> `resolve` -> on 200: `engine.setTimeline(plan)`
  + UI; on 422: keep the previous plan playing, show the problem (`arrange-error`).

## 6. Worker render request (worker-api 1.10.0)

```yaml
RenderJobRequest:            # 007 fields unchanged
  timeline: RenderTimeline | null      # new, optional; null = song order (007 behaviour)
RenderTimeline:
  crossfadeFrames: int                 # even, 0..4410
  segments: RenderSegment[]            # 1..256
RenderSegment:
  sourceStartFrame: int                # >= 0
  frameCount: int                      # >= 1 and > crossfadeFrames
  mutedInputs: int[]                   # unique indices into inputs, 0..63
```

Rules: total frames <= 79,380,000 (`422`); `mutedInputs` index >= `len(inputs)` -> `422`; with a
timeline `range` is in arranged seconds and clamped to the arranged length; output length =
arranged length (or range). Rendering: for each block of arranged frames `[t0, t1)` and each
input, sum the overlapping runs (3.3/3.4; runs are derived from `segments` with `mutes` replaced
by `mutedInputs`), then the 007 mix law, two passes, clip protection and encoders unchanged.
`RenderMetadata.comment` gets `"arranged: <name>"` from the backend.

## 7. Export (backend-api 1.18.0)

`CreateAudioExportRequest` gains (both optional, mutually exclusive; `422 arrangement` when both):

- `arrangementId: uuid | null` - a saved arrangement of the same track;
- `arrangement: ArrangementInput | null` - an unsaved editor state (`{snap, crossfadeMs,
  regions}`, name optional for the contents text).

Planner: the 007 `RenderPlanner` output is unchanged; `TimelineExportMapper` maps the resolved
segments to `RenderSegment`s with `mutedInputs = [i | !open(seg, inputs[i].channelId)]`
(vectors `timeline.spans[*].expected.workerSegments`). Range validation uses the arranged length.

`ExportRecipe` gains `arrangement: { id: uuid | null, name: string, revision: int | null,
timeline: ResolvedTimeline } | null`. Contents text appends `", arranged (<name>)"` (name
truncated to 40 characters with an ellipsis); default item name
`"<track title> - <arrangement name> - <contents>"`. Retry re-renders from the stored timeline,
never from the current arrangement.

## 8. Limits (`ContractLimits` additions)

| Constant | Value |
|---|---|
| `ArrangementNameMaxLength` | 80 |
| `RegionLabelMaxLength` | 40 |
| `MaxRegions` | 64 |
| `MaxRegionRepeat` | 16 |
| `MaxTimelineSegments` | 256 |
| `MaxArrangedSeconds` | 1800 |
| `MinRegionSeconds` | 0.5 |
| `MaxCrossfadeMs` | 50 |
| `DefaultCrossfadeMs` | 10 |
| `MaxArrangementsPerTrack` | 50 |
| `MaxUndoSteps` (UI) | 100 |
| `TimelineSampleRate` | 44100 |
| `BarLineToleranceSeconds` | 0.001 |

## 9. Problem types (new)

`arrangement-limit` (409), `arrangement-conflict` (409, extension member `currentRevision`),
`arrangement-not-found` (404), `track-not-ready` (409, existing), `no-sections` (409, create from
sections when 014 found none), validation (422). Worker: validation (422) only.
