# ADR-015: Synced lyrics with a karaoke ball (feature 006)

- **Status**: Accepted (architect, 006 Phase 0, 2026-11-15; see "Phase 0 outcome")
- **Date**: 2026-11-15
- **Decider**: Architect (autonomous mode), to be confirmed by the CTO
- **Feature**: `specs/006-synced-lyrics-karaoke/`
- **Builds on**: ADR-005 (audio engine), ADR-012 (GPU device policy), ADR-014 (online lookups)
- **Research**: `docs/research/roadmap-003-011.md` section 5, `specs/006-synced-lyrics-karaoke/research.md`

## Context

The owner wants lyrics to be downloaded at import when they are available online and to run
along with the music "like Spotify", with a bouncing karaoke ball in a banner at the bottom of the
screen so the user knows when to sing. LRCLIB offers free line-synced lyrics (LRC) and, for some
records, word timings. When only plain text exists, words can be aligned to the separated vocal
stem with stable-ts (MIT) and Whisper large-v3-turbo (MIT weights). Lyrics are copyrighted.

## Decisions

### D1 Sources and order

1. LRCLIB `GET /api/get` with title, artist, album (when known) and the track duration
   (LRCLIB matches +-2 s), then without album, then `GET /api/search` and our own selection
   (duration within +-2 s; prefer `hasWordSync`, then synced, then plain).
2. Word timing per case: `lyricsfile` word starts when `hasWordSync`; otherwise interpolation of
   the LRC line timings over the words (weight = vowel groups, minimum 1 per word); LRCLIB
   `instrumental: true` -> status `instrumental` (no banner).
3. Plain text only (LRCLIB plain, a synced record whose duration is off by more than 2 s, or text
   pasted by the user) -> **forced alignment** of that text to the vocal stem: stable-ts
   `align(..., original_split=True)` on the `vocals.lead` detail stem when it exists, else the
   `vocals` stem, resampled to 16 kHz mono in the worker (soxr, already locked).
4. Nothing found -> status `not-found`; the user can paste lyrics (-> 3).

Every word carries its timing origin (`source`, `interpolated`, `aligned`), shown in the UI.

### D2 Lookup and alignment are separate worker job lanes

`POST /v1/lyrics-jobs` with `mode: lookup` runs on the ADR-014 network lane (seconds, no model).
`mode: align` runs on the separation scheduler as a model step through the ADR-012
`DeviceRunner` (`GpuNeed("whisper-large-v3-turbo", <measured in Phase 0>)`, one GPU step at a
time, CPU retry on out-of-memory). Priority: standard separation > lyrics alignment > detail
parent steps; a running step is never pre-empted, so an alignment waits at most one detail
parent step. Alignment starts automatically when the worker device is the GPU; on a CPU-only
worker the user starts it with one click (it takes minutes there; the measured time is shown).

### D3 Dependency isolation

`stable-ts` 2.19.1 needs `openai-whisper<=20250625` and `torchaudio`. torchaudio has no 2.15
build; `torchaudio 2.11.1` (BSD-2-Clause, requires `torch>=2.11`) resolves with our
`torch 2.15.0` (verified with `uv pip compile`), and stable-ts uses only `torchaudio.functional`
(resample, filters) and `torchaudio.save`, which we avoid by passing a 16 kHz numpy array. If
Phase 0 shows that torchaudio 2.11.1 fails to import next to torch 2.15, the fallback is a
separate `aligner` container with its own lock (Complexity Tracking). `openai-whisper` pulls
`triton` on Linux x86_64; triton bundles NVIDIA binaries, so the CPU image drops it with a uv
override (`triton; sys_platform == 'never'`, verified) and the GPU image keeps the triton that
torch cu132 already brings (ADR-011). Whisper weights (`large-v3-turbo.pt`, 1,617,941,637 bytes,
SHA-256 `aff26ae4...a19a`, MIT) are downloaded at first use into `STEMSTUDIO_MODEL_DIR/whisper`,
never committed.

### D4 One normalised timed-lyrics document

The worker returns `TimedLyrics` (lines with `startMs`/`endMs`, words with `startMs`/`endMs`
and `origin`, `syncLevel` `word|line|plain`, `language`, `source`, `attribution`). Every word has
an end (`next word start` or the line end, capped at 3 s). The backend stores it per track; the
UI and the engine only render it. No lyrics parsing happens in .NET or TypeScript.

### D5 Karaoke ball timing: requestAnimationFrame on the audio clock

The banner is driven by a new audio-engine module (`karaoke/`), not by Blazor:

- `attachKaraoke(elementId, lyrics, options)` builds the line and word DOM inside a host element
  that Blazor renders empty and never re-renders (`@key`, no child content).
- Each `requestAnimationFrame` tick reads the engine's **audio-clock position**:
  `offset + (ctx.currentTime - when)` (the transport formula of ADR-005), smoothed with
  `ctx.getOutputTimestamp()` (contextTime/performanceTime pair, extrapolated with
  `performance.now()` between audio callbacks), minus `ctx.outputLatency` (when supported), plus
  the user offset. Loop wrap and seek come for free because the position is derived, never
  accumulated.
- Current line and word are found by binary search with a cached cursor (O(1) per frame in
  normal playback). The ball moves on a parabola from word *i* to word *i+1*; x is interpolated
  between the word centres, y = `h * 4p(1 - p)` with `p` the progress through word *i*, so the
  ball lands on each word exactly at its start time. Only `transform: translate3d(...)` and
  `data-state` attributes change per frame (compositor-only, no layout reads in the loop: word
  centres are measured once after layout and on resize).
- No per-frame .NET interop (as for the meters). The loop runs only while attached, playing and
  the document is visible; on pause/seek one frame is rendered. `setInterval`/`setTimeout` are
  never used for the ball (constitution V; the ball is visual, the rule is honoured anyway).
- `prefers-reduced-motion: reduce` -> no bounce; the current word is highlighted instead.

### D6 Copyright handling

Lyrics are shown only inside the app for the user's own imported track, for personal practice.
They are stored in the local database only, never written to logs (only counts), never included
in exports (007), mix files or API list responses, served with `Cache-Control: no-store`, and
deleted with the track. The worker keeps lyrics job results only until the backend has copied
them (`DELETE /v1/lyrics-jobs/{id}`; 24 h sweep otherwise). The banner credits the source
("Lyrics: LRCLIB", or "Lyrics: entered by you"; "timing: automatic alignment" when aligned) and the
personal-practice notice names lyrics. No third-party lyrics are committed to the repository:
tests use self-written fixture text.

### D7 User offset

One offset per track (-5000..+5000 ms, step 10 ms; default 0), stored with the lyrics (not with
mixes, because it describes the lyrics-to-audio relation). Keyboard nudges +-50 ms.

## Consequences

- Engine contract gains an additive minor version (karaoke methods and types).
- Worker gets `lyrics/` (LRCLIB client, LRC and lyricsfile parsers, interpolation) and
  `alignment/` (stable-ts adapter behind a port) packages; new dependencies `stable-ts`,
  `openai-whisper`, `torchaudio`, `tiktoken`, `more-itertools` (all MIT/BSD); PyYAML moves to the
  runtime group for `lyricsfile` (`yaml.safe_load` only).
- One more model download (1.6 GB) on first alignment; disk and VRAM figures are measured in
  Phase 0 and shown to the user before the first download.

## Phase 0 outcome (2026-11-15, combined 006 + 008 Phase 0)

- **D3 confirmed, no aligner image**: torchaudio 2.11.1 imports and runs its resample and
  biquad filters next to torch 2.15.0 in both images (`+cpu` in the CPU lock, `+cu132` from the
  cu132 index in the GPU image, installed by devops); triton is never imported, so the uv
  override keeps the CPU image free of NVIDIA binaries.
- **D2 sizing**: `WHISPER_GPU_NEED = GpuNeed("Lyrics alignment (Whisper large-v3-turbo)", 5120)`
  (NVML peak 4,118 MiB); a 4:22 song aligns in 2.7-6.7 s on the RTX 5070 Ti and in about 3.5 min
  on 8 CPU threads (estimate 0.03 / 0.8 s per audio second + model load, + 140 s for the first
  download).
- **D1.3 refined (stem choice and collapse check)**: alignment on the plain `vocals` stem
  collapsed on the reference song while `vocals.lead` worked (research.md P0-2). The adapter uses
  `vocals.lead` when available, detects a collapse (> 35 % of words under 20 ms, or > 25 % of
  lines overlapping), retries once with `vad=True` and otherwise fails with "The lyrics could not
  be aligned to the vocals."; the UI then offers splitting the vocals first. Automatic alignment
  stays on (it is cheap on the GPU), but its reliability on unsplit vocals is a known limit
  (README, help topic `lyrics-align`).
- Contracts: worker-api 1.5.0, backend-api 1.6.0, audio-engine 2.1.0, ui-test-ids 1.6.0
  (`specs/006-synced-lyrics-karaoke/contracts/`, refinements in `DELTA.md`).
