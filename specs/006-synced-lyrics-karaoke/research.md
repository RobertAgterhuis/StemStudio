# Research: Synced Lyrics with Karaoke Ball

Decisions and feasibility checks for feature 006 (architect, 2026-11-15). Earlier desk research:
`docs/research/roadmap-003-011.md` section 5. Binding design: ADR-015 (builds on ADR-014).

## Feasibility checks (2026-11-15)

Read-only requests with the generic User-Agent `StemStudio/0.1 ( local-practice-tool;
contact-not-configured )`, about 15 to LRCLIB and PyPI plus one HEAD to the Whisper weights.
**No lyric text was copied into the repository**: only structure, counts and timestamps of the
responses are recorded here (lyrics are copyrighted; ADR-015 D6).

### F-01 LRCLIB `GET /api/get`

| Request | Result |
|---|---|
| `track_name=Hero of the Day&artist_name=Metallica&album_name=Load&duration=262` | **200** in 0.13 s: id 546388, album "Load", duration 262.0, `instrumental: false`, **`hasWordSync: true`**, `plainLyrics` 1,435 chars, `syncedLyrics` 2,013 chars (**53 LRC lines**, first `[00:10.95]`, last `[04:07.87]`), `lyricsfile` 16,513 chars |
| same without album, `duration` = 262 / 265 / 258 / 270 / 285 / 200 | 262 -> id 546388 (Load, word sync); 265 -> id 10422352 (263.0 s, single, word sync); 258 -> id 16019288 (260.0 s, compilation, word sync); 270 -> id 13446363 (271.0 s, **no word sync**); 285 -> id 3780737 (S&M live, no word sync); 200 -> **404** "Failed to find specified track". Confirms the per-record **+-2 s** rule and that records of the same song differ in quality. |
| without the `duration` parameter | 200 (some record of the song, version unknown); with an empty `duration=` the body was not JSON. We therefore always send the duration and treat any non-JSON body as "not found" |
| `track_name=Monkeys Spinning Monkeys&artist_name=Kevin MacLeod&duration=125` | **200**: id 3879894, album "Over The Moon", 125.0 s, **`instrumental: true`**, no lyrics, `lyricsfile` 159 chars (metadata, empty `lines`) -> status `instrumental` |
| `GET /api/get-cached` (same parameters) | 404 (cache-only endpoint; not used) |
| Response headers | `access-control-expose-headers: retry-after`; no rate-limit headers on success |

### F-02 LRCLIB `GET /api/search`

`track_name=Hero of the Day&artist_name=Metallica`: 200, 20 records (max), durations 262.0 (13
records, 12 with word sync) and 285.0 (live S&M, 7 records, no word sync). `Monkeys Spinning
Monkeys`: 20 records, all `instrumental`, durations 125.0-130.0 plus a 173 s "(104bpm)" variant.
Selection rule (ADR-015 D1): duration within +-2 s, then `hasWordSync`, then synced, then plain;
ties -> the album equal to ours, else the lowest id.

### F-03 `lyricsfile` structure (word timing)

YAML: `version: '1.0'`; `metadata` {`title`, `artist`, `album`, `duration_ms: 262000`,
`instrumental: false`, `language: en`}; `lines[]` {`text`, `start_ms`, `end_ms`, `words[]`
{`text`, `start_ms`}}. **Words have only `start_ms`**: the word end is the next word's start,
the last word of a line ends at the line's `end_ms` (capped at 3 s, research R-03). The
`language` field gives the alignment language when plain text is aligned.

### F-04 Python packages (PyPI JSON metadata, `uv pip compile` dry resolution)

| Package | Version | Licence | Requires |
|---|---|---|---|
| stable-ts | 2.19.1 | MIT (LICENSE file) | numpy, torch, **torchaudio**, tqdm, `openai-whisper>=20230314,<=20250625` |
| openai-whisper | 20250625 | MIT | more-itertools, numba, numpy, tiktoken, torch, tqdm, **`triton>=2` on Linux x86_64** |
| torchaudio | **2.11.1** (latest; no 2.12-2.15 build exists) | BSD-2-Clause | `torch>=2.11` |
| tiktoken | 0.14.0 | MIT | regex, requests |
| more-itertools | 11.2.1 | MIT | - |

`uv pip compile` (Python 3.12, `x86_64-manylinux_2_28`, PyPI + PyTorch CPU index) of
`torch==2.15.0, stable-ts==2.19.1, tinytag==2.3.2` **resolves**: `torch 2.15.0+cpu`,
`torchaudio 2.11.1+cpu`, `openai-whisper 20250625`, `tiktoken 0.14.0`, `triton 3.9.0`. With the
override `triton ; sys_platform == "never"` triton disappears and nothing else changes (verified),
and no `nvidia-*`/`cuda-*` package appears, so the 003 CPU-image guard stays green.

stable-ts source check (2.19.1 sdist): torchaudio is used only in `audio/utils.py`
(`torchaudio.functional.resample`, `highpass_biquad`/`lowpass_biquad`), `audio/output.py`
(`torchaudio.save`) and the non-Whisper transcribe path. Passing a 16 kHz mono float32 numpy array
avoids resampling and saving. `align(model, audio, text, language, ..., original_split=True)`
keeps our line breaks; `align_words(...)` refines words inside given segment times (P3 option for
line-synced lyrics). Whisper `timing.py` imports triton only for CUDA tensors and falls back to
numba/CPU kernels on `RuntimeError`; with CPU tensors triton is never imported.

**Resolved in Phase 0 (P0-1 below)**, was: whether `torchaudio 2.11.1` imports and runs
`functional.resample`/filters next to `torch 2.15.0` (the wheel is `py3-none`, compiled against
torch 2.11). Not verified here (would need a ~1 GB install). Fallback: a separate `aligner` image
with its own lock (Complexity Tracking in plan.md).

### F-05 Whisper large-v3-turbo weights

`HEAD https://openaipublic.azureedge.net/main/whisper/models/aff26ae4...a19a/large-v3-turbo.pt`:
200, `application/octet-stream`, **1,617,941,637 bytes**; the SHA-256
`aff26ae408abcba5fbf8813c21e62b0941638c5f6eebfb145be0c9839262a19a` is part of the URL and
checked by `whisper._download`. Licence MIT (openai/whisper repository). Not downloaded.

## Decisions

### R-01 Lyrics source: LRCLIB only

- **Decision**: LRCLIB `/api/get` (with album, then without), then `/api/search` + own selection.
- **Rationale**: free, keyless, documented API with synced and word-synced lyrics; measured fast
  (0.1 s). The rest of the "syncedlyrics" ecosystem (Musixmatch, NetEase, Megalobiz scraping; the
  `syncedlyrics` package is already in our lock via spotDL) violates terms or scrapes; Musixmatch's
  official synced lyrics are a paid tier.
- **Licence/terms**: LRCLIB states no data licence or terms (docs, `llms.txt`); the code is MIT.
  We treat the lyrics as copyrighted works: local display only (ADR-015 D6), credit "Lyrics:
  LRCLIB".

### R-02 Rate limits and identification

Requests are sequential on the 005 network lane with >= 500 ms between LRCLIB calls (LRCLIB asks
for 200-500 ms gaps; identified clients ~90/min); `Retry-After` honoured; same User-Agent as 005
(LRCLIB asks for name, version and homepage: the contact URL from `STEMSTUDIO_CONTACT_URL` when
set). At most 3 requests per lookup (get+album, get, search).

### R-03 Word timing normalisation (worker, one implementation)

- **Word sync**: `lyricsfile` words; `end = next.start` within the line; last word `end =
  min(line.end_ms, start + 3000)`.
- **Line sync only**: words = whitespace split (CJK: per character); weight = vowel groups
  `[aeiouyà-ÿ]+` (Unicode-aware, minimum 1); the line span is `[line.start, next line start)`,
  but at most `60 ms * characters + 1500 ms` (so a line before a long instrumental break does not
  stretch); words get consecutive slots proportional to their weights. Origin `interpolated`.
- **LRC parsing**: `[mm:ss.xx]` / `[mm:ss.xxx]` / `[mm:ss]`, several stamps per line, `[offset:]`
  applied (LRC offset: positive = earlier), metadata tags ignored, empty lines = breaks.
- **Limits**: <= 400 lines, <= 40 words per line, <= 500 characters per line, <= 1 MB input; larger
  inputs are rejected as "not usable".

### R-04 Alignment design

- **Model**: Whisper `large-v3-turbo` via stable-ts on both devices (configurable
  `STEMSTUDIO_ALIGN_MODEL`, default `turbo`); `small` is possible on slow CPUs but not default.
- **Audio**: the `vocals.lead` detail stem when the worker cache has it, else `vocals`;
  downmix to mono, resample to 16 kHz with soxr (locked), float32.
- **Device**: through the 003 `DeviceRunner` with `GpuNeed("whisper-large-v3-turbo", <measured>)`
  (estimate ~6 GB; Phase 0 spike P0-2 measures NVML peak, time on GPU and CPU for the reference
  song). CPU fallback on out-of-memory as for Demucs.
- **Scheduling**: a model step in the separation scheduler; priority standard > align > detail
  parent step; never pre-empts. Automatic only when the worker's device is the GPU.
- **Output**: stable-ts `WhisperResult` -> our `TimedLyrics` (words mapped back to our tokens by
  order; stable-ts keeps the given text). Words with `probability < 0.2` or zero duration keep
  their aligned start but are flagged `lowConfidence` (shown in the expert view only).
- **Progress**: stable-ts progress callback -> job progress (`Aligning lyrics`); model download
  progress -> stage `Downloading speech model (1.6 GB)`.

### R-05 Karaoke rendering (engine)

- **Clock**: `Transport.position()` (already derived from `ctx.currentTime`). Smoothing: on each
  frame read `ctx.getOutputTimestamp()`; `audioNow = ts.contextTime + (performance.now() -
  ts.performanceTime) / 1000`; position = `offset + (audioNow - when)` wrapped into the loop,
  minus `ctx.outputLatency` where available (Chrome/Edge/Firefox; Safari: `baseLatency` only),
  plus the user offset. Falls back to `position()` when `getOutputTimestamp` is missing (mock,
  old browsers). Never accumulated from frame deltas, so no drift (SC-002).
- **Frame loop**: one shared `FrameLoop` per engine for meters and karaoke (the meters already
  use `requestAnimationFrame`); runs while anything is attached and the document is visible.
- **Lookup**: cursor (line index, word index) advanced forward in O(1); binary search after a
  seek, loop wrap or offset change.
- **Layout**: word centres measured once after DOM build (`getBoundingClientRect`, relative to
  the banner) and on `ResizeObserver`; per frame only `style.transform` on the ball and
  `data-state` changes on at most two words and lines (no layout reads per frame; SC-004).
- **Ball path**: from the centre of word *i* to word *i+1*; `p = (t - start_i) / (start_{i+1} -
  start_i)` clamped 0..1; `x = lerp(x_i, x_{i+1}, p)`; `y = -h * 4p(1 - p)` with `h` = 0.6 line
  height (shorter for words < 150 ms: `h * dur/150`); across a line change the ball arcs to the
  first word of the next line, which is in the "next" slot until the line becomes current.
- **States**: `hidden` (no lyrics, instrumental, removed), `countdown` (before first line and in
  gaps > 5 s: 3 dots, one per second), `singing`, `ended`.
- **Reduced motion**: `matchMedia('(prefers-reduced-motion: reduce)')` -> no ball; the current
  word gets `data-state=current` with a fill effect.

### R-06 Persistence and copyright

Backend table `TrackLyrics` (1:1 with Track, cascade delete); lines/words as a JSON column.
`GET /api/tracks/{id}/lyrics` only (never in `GET /api/tracks`), `Cache-Control: no-store`;
logging policy: lyrics DTOs are never logged (a test scans log output of a full fake run for the
fixture words, SC-006). Worker: lyrics job results deleted by `DELETE /v1/lyrics-jobs/{id}` after
the backend copied them; a 24 h sweep removes leftovers.

### R-07 Test material (no third-party lyrics)

- Unit tests: self-written lyrics ("Fake line one, la la la ...") as LRC, as `lyricsfile` YAML
  and as LRCLIB JSON shapes (recorded structure from F-01/F-03 with replaced text).
- Alignment test clip (Phase 0, qa): a 20-30 s clip with self-written lyrics sung or spoken by a
  synthetic voice is **not** acceptable for SC-003 (not singing); instead qa records a short
  self-sung clip (owner or team, licensed CC0 by them) or uses a CC0/CC BY vocal recording with
  its own lyrics, plus a manual word-start annotation (`tests/fixtures/lyrics/align-clip.json`).
  Owner question O-2.
- Reference song: validated only on the reference machine (CTO), lyrics fetched at run time.

## Open items for Phase 0 (listed, not executed)

- P0-1: install spike in a scratch venv/container: `torch 2.15.0` + `stable-ts 2.19.1` +
  `torchaudio 2.11.1`; import, `functional.resample`, `stable_whisper.load_model('turbo')`,
  `align()` on a 30 s clip. Decide: same worker image vs separate `aligner` image.
- P0-2: measure on the reference machine: GPU time and NVML peak for aligning the reference
  song's vocal stem (cold/warm), CPU time; set `GpuNeed` and the CPU estimate formula (SC-005).
- P0-3: create the alignment test clip and annotation (R-07, owner question O-2).

## Owner questions

| # | Question | Blocking? |
|---|---|---|
| O-1 | (from 005) public contact page for the User-Agent | No |
| O-2 | For the SC-003 alignment test clip: may the team record a short self-sung clip with self-written lyrics (licensed CC0 by the singer), or should qa pick an existing CC0/CC BY vocal recording? | Blocks only the SC-003 integration measurement (T012/T042/T066), not the wave |

## Phase 0 results (2026-11-15, T001/T002)

Throw-away containers from `stemstudio/worker:local` (CPU, torch 2.15.0+cpu) and
`stemstudio/worker:local-gpu` (torch 2.15.0+cu132, triton 3.9.0 in the image); extra packages
installed with `pip --target /tmp/pk --no-deps` inside the container only (the image's torch was
never replaced); volumes read-only; the running `stemstudio` project untouched; `nvidia-smi`
showed 13.6-14.1 GB free before every GPU run. Lyrics were fetched from LRCLIB at run time into
the scratch directory, never printed and deleted afterwards (only counts and times are recorded).

### P0-1 install spike: **same worker image** (no aligner image)

| Check | CPU image | GPU image |
|---|---|---|
| torchaudio wheel | `2.11.1+cpu` (whl/cpu) | `2.11.1+cu132` (whl/cu132; cu130/cu128 stop at 2.11.0) |
| `import torchaudio` next to torch 2.15.0 | 0.02 s, no warning | same |
| `functional.resample` 44.1 -> 16 kHz, `highpass_biquad`, `lowpass_biquad` | run | run |
| `import stable_whisper` (2.19.1), `whisper` (20250625) | 0.15-0.19 s | same |
| triton imported? | never (import, `load_model`, `align`) | never (CUDA path did not need it) |
| `load_model("turbo", download_root=...)` first use | 136.4 s incl. the 1,617,941,637-byte download, SHA-256 `aff26ae4...a19a` verified by whisper (reads the whole file into RAM once) | |
| cached model load | 12-13.5 s | 17.3-18.8 s |

Consequences: the CPU lock (`uv.lock`) carries `torchaudio 2.11.1+cpu` and no triton (override);
**devops** installs `torchaudio==2.11.1+cu132` from the cu132 index in the GPU image next to its
torch cu132 (same rule as torch itself, ADR-011).

### P0-2 measurements (reference song, 261.9 s, 48 lines, 285 words)

`align(audio16k_mono_f32, text, language="en", original_split=True)`; audio prep (FLAC read, mono,
soxr to 16 kHz) 0.35-0.40 s.

| Run | Wall time | s per audio s | Memory |
|---|---|---|---|
| GPU, `vocals.lead`, cold / warm | 6.7 / 5.0 s | 0.026 / 0.019 | NVML peak delta **4,059 MiB** (context included); torch max allocated 3,581, reserved 3,692 MiB |
| GPU, `vocals`, cold / warm | 4.8 / 2.7 s | 0.018 / 0.010 | NVML peak delta **4,118 MiB** |
| CPU 8 threads, `vocals.lead`, whole song | 207.8 s | 0.79 | - |
| CPU 8 threads, first 60 s | 76.2 s | 1.27 | - |

Set from these: `WHISPER_GPU_NEED = 5120 MiB` (4,118 + 20 %, rounded up to 256);
`estimate_alignment_seconds` = duration x (GPU 0.03 | CPU 0.8) + (GPU 20 | CPU 15) s + 140 s while
the weights are not cached (SC-005: GPU well under 2 min per 4-min song).

**Quality finding (binding for stem-separation T040/T041)**: against the LRCLIB line starts,
`vocals.lead` gives a median line-start error of 405-415 ms (15 of 48 lines within 300 ms, 29
within 500 ms; part of it may be LRC stamps leading the sung onset). The plain `vocals` stem
**collapsed** (all lines squeezed into 12.5-90 s, median error 83.6 s) although it is nearly
the same audio (difference -41.7 dB): Whisper alignment is fragile, not the stem. Tried on
`vocals`: `vad=True` (median 43.7 s), `only_voice_freq` (52.0 s), `fast_mode` (104 s),
`nonspeech_error=0.1` (no change), gain -1 dB, peak normalisation, `original_split=False`: all
collapse; `vad=True` on `vocals.lead` is worse (543 ms). stable-ts `failure_threshold=0.2` did
not reject the collapsed run (279 of 285 words with zero duration). A collapse detector separates
the cases cleanly: words shorter than 20 ms are 9-16 % of a good run and 69-98 % of a collapsed
one. Adapter rule (ports `COLLAPSE_*` constants): align on `vocals.lead` when the worker detail
cache has it, else `vocals`, with default stable-ts options; a run with more than 35 % of words
under 20 ms or more than 25 % of lines starting before the previous line ends is *collapsed*;
retry once with `vad=True`; a second collapse fails the job with `ALIGNMENT_FAILED_TEXT`, and the
track page (frontend T046/T047) offers "Split the vocals into detail stems and try again" when
the vocals are not split. Accepted runs mark words under 20 ms or with probability < 0.2 as
`lowConfidence`. SC-003 is measured on `vocals.lead`.
