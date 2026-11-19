# StemStudio

[![ci](https://github.com/RobertAgterhuis/StemStudio/actions/workflows/ci.yml/badge.svg)](https://github.com/RobertAgterhuis/StemStudio/actions/workflows/ci.yml)
[![pages](https://github.com/RobertAgterhuis/StemStudio/actions/workflows/pages.yml/badge.svg)](https://github.com/RobertAgterhuis/StemStudio/actions/workflows/pages.yml)

**Website: <https://robertagterhuis.github.io/StemStudio/>** (screens, feature status, manuals;
rebuilt from this repository on every push to `main`).

Practise along with your favourite songs. StemStudio imports a song (YouTube link, Spotify track
link or a local MP3/WAV/FLAC file), splits it into instrument stems with
[Demucs](https://github.com/adefossez/demucs), and gives you a mixer with a beat-synced
metronome so you can mute the guitar and play it yourself.

> **Personal practice only.** You are responsible for complying with copyright law and with the
> terms of service of YouTube and Spotify. StemStudio never circumvents DRM: for Spotify links it
> only reads track metadata and fetches a matching public recording from YouTube. Covers, band
> texts and lyrics belong to their rights holders: StemStudio shows them in the app only and never
> puts them into exports or sheet music.

## Features

- **Import** from YouTube, Spotify (metadata → YouTube match) or a local file (up to 200 MiB,
  15 minutes), with progress reporting and a per-source cache.
- **Stem separation** into vocals, drums, bass, guitar, piano and other (`htdemucs_6s`), with a
  fallback to 4 stems (`htdemucs`).
- **Mixer**: per stem a volume fader, pan, mute, solo and a post-fader peak meter; master volume;
  play/pause/stop/seek; A-B loop. All stems play sample-locked.
- **Metronome** on the detected beat grid (librosa) with accent on beat 1, its own volume, and
  manual BPM / offset / beats-per-bar correction. Clicks are scheduled on the Web Audio clock.
- **Mixes**: save, load, edit, duplicate and delete named mixes (all channel, master, metronome
  and loop settings).
- **Detail separation** (feature 002): drums, vocals and other stems are split further where
  the song allows it (kick/snare/toms/hi-hat/cymbals, lead/backing vocals, instrument families).
- **GPU acceleration** (feature 003, optional): an NVIDIA GPU makes separation about 8x faster.
- **Design system and journeys** (feature 004): Beginner, Advanced and Expert levels, in-app help
  and tours, keyboard shortcuts, light and dark theme.
- **Cover art and band info** (feature 005): covers and artist facts from open data services.
- **Synced lyrics** (feature 006): a karaoke banner with a bouncing ball under the mixer.
- **Library and exports** (feature 007): save stems, mixes and stem packs as FLAC, WAV or MP3.
- **Sheet music** (feature 008): a PDF lead sheet plus MusicXML and MIDI per instrument.
- **Style transfer** (feature 009, optional, NVIDIA GPU): restyle a song or a range of it with a
  style from a library of 24 presets or your own styles (ACE-Step 1.5).
- **Songs from your own lyrics** (feature 010, optional, NVIDIA GPU): write lyrics, generate a
  sung song, and optionally convert the vocal into your own voice after explicit consent.
- **AI composer agent** (feature 011, optional): type a command in Dutch or English ("mute the
  drums and loop the chorus", "importeer deze link en splits verder") and a local language model
  (Ollama) plans and carries it out with StemStudio's own tools; heavy and destructive steps ask
  for your confirmation first.
- **Practice tempo** (feature 012): slow the whole song down to 50 % or speed it up to 120 %
  without changing its key, transpose it by up to 12 semitones without changing its speed, a
  speed trainer that loops A-B and gets faster every few passes, and a one- or two-bar count-in.
  Saved with the mix; exports keep the original speed and key.
- **Library care** (feature 013): back up the whole library (database, stems, mixes, exports,
  sheet music, generations, voice profiles with their consent) into one ZIP in a backups folder,
  check and download it, and restore it into an empty library, in place of the current one (with
  undo) or by adding only what is missing. A storage overview shows where the disk space goes and
  safely removes regenerable files (detail stems, worker caches, leftovers), never originals.
- **Library by artist and album** (feature 022): every saved item is filed automatically in an
  Artist > Album > Song folder (in the app only, nothing on your disk), with folder pictures from the
  cover art you already have, grid or list, and a live search over artist, album, song and
  stem or instrument ("snare", "vocals", "keyboard"). Wrong names are corrected with library info.
- **Lyric writing assistant** (feature 023): on demand, checks your own lyrics sheets in Dutch or
  English for spelling, grammar and flow, suggests rhymes and poetic devices, writes alternatives,
  continuations and title ideas, counts syllables against a rhythm target and finds rhymes. Poetic
  licence is never an error. Spelling, syllables and rhymes use dictionaries on your computer; the
  rest uses the agent's local model (or Claude, only after a notice). Own lyrics only.
- **Practice flow** (feature 014): the song's sections (intro, verse, chorus ...) are found after the
  separation and shown as a lane in the mixer; one click loops a section, sections can be edited. The
  score follows playback bar by bar, setlists play song after song (with a PDF for the music stand),
  every practice command has a key you can change (foot pedals that send keys work too), and an
  optional practice log on this computer shows what you practised.
- **Play-along recording** (feature 015): record yourself over the mix (vocals, guitar or bass, acoustic,
  keys in stereo, drums) with latency calibration, loop takes, a Takes panel, compare take and original,
  fine timing adjustment and export with the take. Recordings stay on this computer.

### Online lookups (feature 005, 006)

After an import the worker looks the song up in MusicBrainz, the Cover Art Archive, Wikidata,
Wikipedia and (for lyrics) LRCLIB. MusicBrainz gets only the title, artist, duration and ISRC;
the other services get the ids found there; LRCLIB gets the title, artist, album and duration.
No audio and no personal data is sent. Requests are throttled (about one per second per service)
and carry a User-Agent naming StemStudio; set `STEMSTUDIO_CONTACT_URL` to a public page about your
install to add it (never an e-mail address). Switch all lookups off with
`STEMSTUDIO_ONLINE_LOOKUPS=off` for the worker. Pictures, band texts and lyrics are shown in the
app only and never written into exports.

### Synced lyrics and the karaoke ball (feature 006)

Synced lyrics from LRCLIB play in a banner at the bottom of the mixer; a ball lands on each word
on the audio clock, also while looping and seeking. Plain lyrics, or lyrics you paste yourself,
are aligned to the vocal stem with Whisper large-v3-turbo (1.6 GB, downloaded at the first
alignment): automatically on a GPU (about a minute), after a click on a CPU (the button shows the
estimate). `STEMSTUDIO_ALIGN_AUTO=gpu|always|never` and `STEMSTUDIO_ALIGN_MODEL=turbo|small` tune
this. Lyrics are copyrighted: StemStudio shows them inside the app only and never exports, logs
or shares them.

### Library and exports (feature 007)

Save a raw stem, a stem pack or a mix ("what I hear" or selected channels) to the library as
FLAC (default), WAV or MP3, 44.1 kHz stereo. Without a choice, stems are 16-bit (bit-identical to
the separated stem) and mixes 24-bit; MP3 is 320 kbps and carries no tags. A mix that would clip
is lowered just enough (peak -0.1 dBFS) and the item says by how much. The worker renders from a
**read-only** mount of the API data volume (`/library`); on the non-Docker path point
`STEMSTUDIO_LIBRARY_DIR` at the API data directory and keep it read-only for the worker
(check with `scripts/check-library-mount.{sh,ps1}`).

### Sheet music (feature 008)

Turn a stem or a pitched detail stem into a practice lead sheet: an A4 PDF plus MusicXML and MIDI,
on the song's beat grid. Profiles: vocals, bass, guitar, keys, piano (accurate or fast) and drums.
Drums need split drums (detail separation); the dialog offers to split them first. The accurate
piano profile uses a 172 MB CRNN model (CC BY 4.0, downloaded at first use); on an 8-thread CPU it
takes about two minutes for a 4-minute song, on a GPU well under a minute. Limits: no triplets,
swing, dynamics, articulations, lyrics, chords or tablature; the key is estimated with a
confidence and can be overridden at Advanced level.

### Style transfer and songs (features 009 and 010, optional)

An optional generator service (ACE-Step 1.5, MIT) restyles a song or a range of it into another
style and generates complete songs with vocals from your own lyrics; a singer service
(SoulX-Singer, Apache-2.0) converts a generated vocal into your own voice. Both need an NVIDIA
GPU (about 8.5 GB free for a low-memory restyle, 14 GB for the standard profile) and are started
with an extra override; without it the controls are disabled and say why:

```bash
docker compose -f docker-compose.yml -f docker-compose.gpu.yml -f docker-compose.generative.yml up --build
```

Measured on an RTX 5070 Ti: a 30 s restyle in about 16 s, a 45 s song in about 51 s (cold), a
10 s voice conversion in about 26 s. Every take is labelled AI-generated in the app and in the
file tags (`AI_GENERATED`, `AI_VOICE`). Styles describe music, never artists: imitation phrases
and artist names are refused. A voice profile needs your own recording (10-60 s) and a signed
consent statement; library audio is refused as a voice sample. Results are for personal use.

### Practice tempo (feature 012)

The **Practice tempo** panel in the mixer changes the speed (50-120 %, keys `-` / `=`) and the key
(-12..+12 semitones, keys `,` / `.`) of the whole mix; every stem stays in sync and the click,
the karaoke ball and the position follow the stretched music. **Original speed and key** (`0`; feature 014 moved it from `R`, which now records)
goes back. At Advanced level the **speed trainer** (`T`) loops the A-B part from a start speed up
to a target in steps, and the **count-in** (`Shift+K`) gives one or two bars of clicks before playing,
optionally before every loop pass. Time stretching uses SoundTouch 2.4.1 (LGPL-2.1) in a
separate, replaceable `soundtouch.wasm`; the web image builds it offline from a pinned source
(`scripts/build-soundtouch.{sh,ps1}` for the non-Docker path) and serves the licence and complete
source at `/third-party/soundtouch/index.html`.

### Library care (feature 013)

**Library care** (navigation) has two tabs. **Backups** writes a consistent backup of the running
library to the backups folder: `./backups` next to `docker-compose.yml`, or the folder in
`STEMSTUDIO_BACKUP_HOST_DIR`. Create it before the first start; on Linux it must be writable by
the API's user (`sudo chown 1654 backups`). Profiles: complete, or smaller without the regenerable
detail stems; Advanced users tick each part. Restore checks the whole archive first (sizes,
SHA-256, layout, database, consent) and changes nothing until that passes; a replace keeps the
old library aside for **Undo restore** until you discard it. Older archives are migrated; archives
from a newer StemStudio are refused. **Storage** lists the free space per area, the size and class
(original or regenerable) of every part, the largest tracks and the model caches, and offers three
clean-ups with a preview. Archives are not encrypted, and a stored Claude key must be entered again
on another computer.

### Library by artist and album (feature 022)

**Library** opens the folders: artists, then albums (by year), then songs, with a path at the top,
**Grid** or **List**, and one search box on every level (press `/` to jump to it). Names come from
the first source that has them: names you set with **Library info**, the band info found for the
track (feature 005), the import, or the fallback *Unknown artist* / *Unknown album*; names that
differ only in capitals, accents, quotes or a leading "The" share a folder. Songs made from your
own lyrics go to *My songs*. Folders are derived from the items (no folder is stored, none is
created on disk) and the pictures reuse the track covers; **All items** (Advanced) keeps the flat
list with its filters. Items are filed when they are saved; existing items are filed by a background
step 2 s after the API starts (`StemStudio:LibraryBackfillBatchSize`, default 500).

### Lyric writing assistant (feature 023)

Open **Lyrics**, press a sheet's title and then **Assistant**. Nothing is checked until you ask
(**Check**, **Suggest devices**, **Alternatives**, **Continue**, **Title ideas**); every result is a
suggestion you accept, reject or mark as intentional, and every accept is one undoable revision.
Spelling uses vendored dictionaries (OpenTaal Dutch, SCOWL English, CMUdict for syllables and
rhymes; 7.7 MB in the API image, licences in `docs/THIRD-PARTY-LICENSES.md`), grammar, flow and
writing help use the agent backend of Settings (local Ollama by default; while the GPU is busy the
model help is unavailable and the spelling check still works). With Claude selected, the text of
the sheet is sent to Anthropic only after you agree to a notice. The assistant refuses sheets that
match lyrics of imported tracks (confirm your own work in the dialog), withholds copies of library
lyrics and refuses "write like <artist>". For tests, `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT` names
scripted model answers (fake stack only).

### AI composer agent (feature 011, optional)

The **Agent** button in the header opens a panel on every page. The agent turns a command into a
visible plan, runs StemStudio's tools step by step (import, split further, mixer, loops, mixes,
exports, lyrics, sheet music, style transfer and songs when those features are available) and
shows each step's result. Heavy steps (separation, transcription, generation) and destructive
steps (deletes, overwrites) always show a confirmation card with the exact arguments; Undo
reverses the agent's last command group where an inverse exists (mixer values, a saved or
overwritten mix, a started job). Conversations are stored locally and restored after a reload.

**Prerequisites.** Ollama on the host (not in compose; https://ollama.com, MIT) and the models,
pulled by you once (Qwen3.5 weights, Apache-2.0):

```bash
ollama pull qwen3.5:9b     # default model, ~6.6 GB, needs tool calling
ollama pull qwen3.5:4b     # fallback, ~3.3 GB, used while a GPU job holds the GPU
curl http://localhost:11434/api/tags   # both must be listed
```

The api container reaches the host at `http://host.docker.internal:11434`; set
`STEMSTUDIO_OLLAMA_URL` for another address (without Docker: `StemStudio__Agent__Ollama__BaseUrl`).
Without Ollama the app works as before and the panel says why the agent is off and how to fix
it. Before a heavy GPU job starts, StemStudio asks Ollama to unload the agent model so the job
gets the GPU memory.

**Settings page** (rail -> Settings): choose the backend - **Local (Ollama)**, the default;
**Claude** (optional, paid per use); or **None** (agent off) - pick the models, and test the
connection. A model that is not pulled or cannot call tools is refused with the `ollama pull`
command to run. `STEMSTUDIO_AGENT_BACKEND=ollama|claude|none` sets the initial backend.

**Claude (optional, paid per use).** Claude can only be selected when a usable key exists. Either
paste it in **Settings -> Composer agent -> Claude API key -> Save key** (open StemStudio as
`http://localhost:8080` on the computer itself; the key is stored encrypted, only its last four
characters are shown, and **Test key** checks it on request), or set `ANTHROPIC_API_KEY` in the
environment of the API for one shell session - never in `.env`, a compose file or a script. The
environment variable wins over a stored key:

```powershell
$env:ANTHROPIC_API_KEY = Read-Host -MaskInput   # bash: read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
docker compose up -d                            # the api is recreated with the key
```

The stored key's encryption keys live in the Docker volume `stemstudio_api-keys` (never copy it
with the library or a backup; without it the key must be entered again). See `SECURITY.md` for what
the encryption does and does not protect. The key is never logged or sent to the browser, and it is
never stored in plain text. Saving a key does not select Claude. Selecting Claude requires acknowledging a
notice once. Only text is sent to Anthropic: the system prompt, the tool definitions, your
messages (the newest part of the conversation) and the tool results (ids, names, statuses,
numbers, short descriptions) - never audio, files, the key, online lyrics or band biographies.
Each run is capped at 60,000 input and 8,000 output tokens (configurable); the panel shows the
usage. StemStudio never switches to Claude by itself.

## Quick start (Docker)

Requirements: Docker with Compose v2. Nothing else; no accounts or keys.

```bash
docker compose up --build
```

Open <http://localhost:8080>. The first real separation downloads the Demucs model weights
(about 50 MB for `htdemucs_6s`) into the `model-cache` volume; later runs are offline for the model.

Useful variants:

```bash
STEMSTUDIO_SEPARATOR=fake docker compose up --build   # instant fake "stems" (demo / tests)
docker compose -f docker-compose.yml -f docker-compose.gpu.yml up --build   # NVIDIA GPU (see below)
docker compose down        # stop; data stays in the named volumes
docker compose down -v     # stop and delete all tracks, stems, mixes and the model cache
```

Ports: web `8080`, API `8081`. The worker is internal to the compose network.

### NVIDIA GPU (optional)

On a machine with an NVIDIA GPU (tested: RTX 5070 Ti 16 GB, Windows 11 + Docker Desktop/WSL2,
driver 610.88) the GPU variant separates about 8x faster (reference song 4:22: standard + detail
separation 3.7 min instead of 31.5 min on the CPU).

```bash
docker compose -f docker-compose.yml -f docker-compose.gpu.yml up --build
./scripts/gpu-smoke.ps1      # or scripts/gpu-smoke.sh: checks CUDA, ONNX Runtime and /health
```

- Requirements: a current NVIDIA driver on the host; on Windows nothing has to be installed inside
  WSL. The GPU image is ~11.5 GB (CPU image ~3.4 GB).
- The import page shows the processing device; each track shows where it was processed.
- One GPU-heavy step runs at a time. Before each step the free GPU memory is checked (the largest
  detail model needs ~10.6 GB); if another program (a game, Ollama) holds too much, that step runs
  on the CPU and the track page says why. A GPU out-of-memory error retries the step on the CPU.
- The GPU image contains NVIDIA's free but proprietary CUDA runtime libraries (system dependency,
  constitution 1.2.0); the default CPU image is fully open source.

## Running without Docker

Requirements: .NET SDK 10.0.401+, [uv](https://docs.astral.sh/uv/) (installs Python 3.12),
Node.js 22, and ffmpeg/ffprobe on `PATH` (plus [Deno](https://deno.com) for yt-dlp's YouTube
support).

```powershell
./scripts/dev.ps1            # Windows: builds the audio engine, starts worker, API and web
./scripts/dev.ps1 -Fake      # same, with the fake separator
```

```bash
./scripts/dev.sh             # Linux/macOS equivalent (--fake for the fake separator)
```

Manual start, configuration variables and validation scenarios are in
[`specs/001-stemstudio-mvp/quickstart.md`](specs/001-stemstudio-mvp/quickstart.md).

## Architecture

```text
Browser ──Blazor Server (SignalR)──► StemStudio.Web (:8080)  Blazor + Radzen UI
   │                                     │ typed HttpClient
   │ fetch FLAC stems (CORS, Range)      ▼
   └──────────────────────────────► StemStudio.Api (:8081)   ASP.NET Core Minimal API
   Web Audio engine (TypeScript)        │  EF Core + SQLite, file storage, JobSyncService
                                        │  HTTP + polling (OpenAPI contract)
                                        ▼
                                  stemstudio_worker (:8000) Python 3.12 / FastAPI
                                  yt-dlp · spotDL · ffmpeg · Demucs · librosa
```

| Component | Path | Responsibility |
|---|---|---|
| Domain | `src/StemStudio.Domain` | Tracks, stems, beat grid, mixes, source keys, validation (no infrastructure) |
| Contracts | `src/StemStudio.Contracts` | DTOs shared by API and Web |
| Persistence | `src/StemStudio.Persistence` | EF Core SQLite, migrations, repositories |
| API | `src/StemStudio.Api` | HTTP endpoints, worker client, job sync, stem serving |
| Web | `src/StemStudio.Web` | Blazor Web App (Interactive Server) with Radzen |
| Audio engine | `src/audio-engine` | Web Audio playback, mixer, meters, loop, metronome (JS interop) |
| Worker | `worker/` | Ingest, separation, beat analysis, job queue |

Dependencies flow one way (Web → Contracts; Api → Persistence → Domain; Api → Contracts); this is
checked automatically by `tools/quality`. Contracts live in
[`specs/001-stemstudio-mvp/contracts/`](specs/001-stemstudio-mvp/contracts/) (OpenAPI 3.1 for the
API and the worker, plus the audio-engine interop contract). Design decisions are recorded in
[`docs/decisions/`](docs/decisions/).

## Manuals

- User manual: [`docs/public/StemStudio-User-Manual.pdf`](docs/public/StemStudio-User-Manual.pdf)
- Technical manual: [`docs/public/StemStudio-Technical-Manual.pdf`](docs/public/StemStudio-Technical-Manual.pdf)

Online lookups (MusicBrainz, Wikimedia) identify StemStudio with a contact URL; set
`STEMSTUDIO_CONTACT_URL=https://github.com/RobertAgterhuis/StemStudio` (or your own public page)
in the environment. Without it a generic User-Agent without personal data is used.

## Development and quality gates

```powershell
./scripts/test-all.ps1       # everything CI runs: build, lint, tests + coverage, SIG gate, e2e lint
```

- Coverage gate: at least 90% line and branch per component (.NET via coverlet, Python via
  pytest-cov, TypeScript via Vitest).
- Maintainability (SIG-style limits): `uv run --project tools/quality python tools/quality/run_all.py`.
  The report is in [`docs/quality/SIG-REPORT.md`](docs/quality/SIG-REPORT.md).
- End-to-end: Playwright in [`e2e/`](e2e/) against the compose stack
  (see [`docs/testing/TEST-STRATEGY.md`](docs/testing/TEST-STRATEGY.md)).
- Website: [`tools/site/`](tools/site/) generates [`site/`](site/) into `site/dist`;
  [`.github/workflows/pages.yml`](.github/workflows/pages.yml) deploys it to GitHub Pages on every
  push to `main`, and `node tools/site/check.mjs` (test-all and CI) validates it.
- CI: [`.github/workflows/ci.yml`](.github/workflows/ci.yml). The quality gates are local: the
  SIG check in [`tools/quality/`](tools/quality/), coverage, linters and type checkers. The
  project uses no SonarQube or Sigrid
  ([ADR-038](docs/decisions/ADR-038-no-sonarqube-sigrid.md)).

The project was built with GitHub Spec Kit: the constitution, specification, plan and tasks are
in [`.specify/memory/constitution.md`](.specify/memory/constitution.md) and
[`specs/001-stemstudio-mvp/`](specs/001-stemstudio-mvp/).

## Known limitations

- **Stem granularity.** Current open-source models separate at most six stems (vocals, drums,
  bass, guitar, piano, other). Separating every instrument individually (for example two guitars,
  or keys versus synth) is not reliably possible; such parts end up together in one stem,
  usually "other". The quality of the guitar and piano stems varies between songs.
- **Processing time.** Separation runs on the CPU by default; a 4-minute song can take several
  minutes. Jobs are processed one at a time.
- **Beat detection** assumes a mostly constant tempo. Songs with tempo changes, rubato or a strong
  backbeat may need manual BPM/offset correction; downbeats can land on the wrong beat.
- **Spotify** links rely on spotDL, which uses unofficial Spotify web clients for metadata, and on
  finding a matching YouTube recording; a track without a match is rejected at import. Playlists,
  albums and podcasts are not supported.
- **YouTube** downloads depend on yt-dlp keeping up with YouTube changes; update the worker image
  when downloads start failing.
- **Browsers**: tested on Chromium and Firefox; Safari is best effort.
- **Single user**, local only: no accounts, no authentication. Do not expose the ports to the
  internet.
- **Lyrics** come from LRCLIB only; a song that is not there needs pasted text. Automatic
  alignment quality varies with the vocal stem (words marked low-confidence at Expert level).
- **Sheet music** is a practice aid: transcription misses or adds notes, detail stems leak, and
  unreliable beat grids need a manual tempo, metre and first downbeat.
- **Generative features** need an NVIDIA GPU (no CPU fallback) and about 10 GB (ACE-Step) plus
  3.3 GB (SoulX) of model weights; the generator image is ~15 GB, the singer image 7-8 GB.
  Results vary per take, lyrics are not always sung exactly as written, and the voice conversion
  works best with 20-30 s of dry singing.
- **AI composer agent** quality depends on the local model: `qwen3.5:9b` handles the evaluation
  set's commands most of the time, not always - check the plan and the confirmation cards. A run
  interrupted by an API restart is marked interrupted; there is no "Continue" button yet - send
  the command again. Track titles from online sources count as untrusted text, so once a run has
  searched the library every following change or heavy step asks for confirmation, even when you
  would not expect it. Undo covers only actions with an inverse; imports, exports and generated
  results are removed with a (confirmed) delete instead.
- **Detail separation** (drums -> kick/snare/toms/hihat/cymbals, vocals -> lead/backing,
  guitar/piano/other -> instrument families) is experimental: quality varies per song, stems
  leak into each other, organ is part of "keys", and on the CPU it takes about 20 minutes for a
  4-minute song (first use downloads ~1.9 GB of models; Docker needs >= 20 GB memory).
- **Backups** are not encrypted and are written to one folder at a time; copying them to another
  drive is up to you (no schedule or cloud target). One library job (backup, check or restore)
  runs at a time, a restore needs an idle library, and a merge restore never overwrites items you
  already have.
- **Practice tempo** is a time stretch, not a re-recording: below 70 % attacks get smeary and
  beyond 7 semitones voices sound unnatural (no formant correction); while it is engaged the
  sound is about 0.07-0.15 s behind the meters. The speed trainer counts every pass, it cannot
  hear whether you played cleanly.
- **Library folders** are derived from names that come from online lookups and tags, so two bands
  with the same name share a folder until you rename one with library info. There are no artist
  photos, and no folder download or rename-a-whole-folder action.
- **Lyric assistant.** The local model is weaker in Dutch than in English, a 40-line sheet takes
  roughly half a minute to a minute, and suggestions can be wrong or mislabel a deliberate choice:
  read each one before accepting. Only Dutch and English, sheets up to 4,096 characters. Lyrics that
  are not in your library cannot be recognised as copies. While a GPU job runs the model help is
  unavailable (no CPU fallback), and Dutch rhymes use word base forms.

## Licences

> **Model weights: local, personal, non-commercial use only.** The pretrained separation models
> (Demucs `htdemucs_6s`/`htdemucs` and the detail models MDX23C DrumSep, UVR MDX-Net KARA 2 and
> MVSep Mega 53-stem) have non-commercial, unclear or no stated licences. They are downloaded on
> first use and are never part of this repository. Commercial use and redistributing images or
> volumes that contain them are not covered. Detail separation credits Ultimate Vocal Remover
> (UVR) and its developers, and ZFTurbo's Music-Source-Separation-Training. The lyrics and
> sheet-music models (Whisper large-v3-turbo MIT, basic-pitch Apache-2.0, piano CRNN CC BY 4.0 by
> Kong et al.) and the generative models (ACE-Step 1.5 MIT; SoulX-Singer, RMVPE and Whisper base
> Apache-2.0) have open licences and are also downloaded on first use.
> The agent's local models (Qwen3.5 9B/4B, Apache-2.0) are pulled by you with Ollama and are
> not part of StemStudio either.

StemStudio's code and libraries are open source only; see
[`docs/THIRD-PARTY-LICENSES.md`](docs/THIRD-PARTY-LICENSES.md), including the GPL/LGPL components
pulled in by spotDL, Demucs and librosa, and SoundTouch (LGPL-2.1, feature 012), shipped as a
separate, replaceable WebAssembly module with its complete source. The lyric assistant (feature 023) uses WeCantSpell.Hunspell (MPL-1.1, unmodified) and vendored
OpenTaal (BSD-3-Clause), SCOWL (permissive) and CMUdict (BSD-2-Clause) word lists.
