# ADR-019: Lyrics to song, own-voice conversion and consent (feature 010)

- **Status**: Accepted (2026-11-15, 009/010 Phase 0; the fourth container `singer` and the consent
  wording are CTO decisions in `docs/decisions/CTO-DECISIONS-2026-11-15.md`).
- **Context documents**: `specs/010-lyrics-to-song/{spec.md, plan.md, research.md, data-model.md}`,
  ADR-018 (generator service, GPU lease, style library, guardrail), `docs/research/roadmap-003-011.md`
  sections 8-9, CTO brief 2026-11-15.

## Context

The owner wants to turn own lyrics into a song with music and a voice, and pointed at VoiceStudio
(debpalash/voicestudio) for sung lines. Research and the spike found: VoiceStudio is AGPL-3.0 and
has no singing synthesis in its code; its default OmniVoice weights are CC BY-NC. ACE-Step 1.5
(ADR-018) sings natively from lyrics. SoulX-Singer (Apache-2.0 code and weights) offers
SoulX-Singer-SVC, an audio-to-audio singing voice conversion that keeps melody, timing and words
and takes the timbre of a short prompt recording. The CTO decided: ACE-Step for the song,
SoulX-Singer-SVC for "my own voice" with an explicit consent checkbox, VoiceStudio not copied.

## Decisions

### D1 Song generation = ACE-Step text2music in the generator (contract 1.1.0)

The generator (ADR-018) gains job kind `song`: lyrics (with section markers), caption from the
style library (ADR-018 D7) plus vocal options, optional BPM, key, time signature, language,
duration, instrumental flag, takes. It loads the 1.7B language model (pt backend) in addition to the DiT for
this kind when memory allows (ACE-Step "thinking" planner for structure and metadata), else runs
the DiT alone (D6); it is released with the DiT when idle. Lyrics are passed through as text; the API normalises them (strip LRC timestamps, CRLF
to LF, trim, at most 4,096 characters) and validates section markers.

### D2 Own voice = SoulX-Singer-SVC in a second service `singer` (same code base)

Pipeline P2 of the research: song take -> import as track -> standard separation by the worker
(Demucs, GPU via the 003 policy) -> job kind `voiceConversion` with the vocal stem as target and
the voice profile sample as prompt -> SoulX-Singer-SVC (F0 by RMVPE for prompt and target,
`auto_shift` to the prompt's register, 32 steps, cfg 3, fp16 on CUDA) -> the converted vocal is
summed with the other five stems at unity -> new take. SoulX-Singer is a pinned code dependency
(git commit, Apache-2.0); only its SVC inference modules and the RMVPE F0 extractor are used; its
preprocessing pipeline (vocal separation, ASR, MIDI transcription) is not installed.

The spike (research.md S-05) found that SoulX-SVC **fails with ACE-Step's transformers 4.57.6**
(`LlamaDecoderLayer` calling convention) and works with transformers 4.41.2 on the same torch
2.10.0+cu128. Therefore SVC runs in a **second service `singer`**: the same `stemstudio_generator`
package and contract, built as a second image variant (`GENERATOR_VARIANT=soulx`, own lock group:
torch 2.10.0+cu128, transformers 4.41.2, librosa, omegaconf; no ACE-Step). `/health` lists the
job kinds a service supports (`kinds`); the API routes `restyle`/`song` to the generator URL and
`voiceConversion`/voice-sample checks to the singer URL. Both services share the GPU lease file,
the model cache volume and the GPU gate code. Rejected: patching SoulX for transformers 4.57
(modifies upstream model code); two transformers versions in one image via `PYTHONPATH`
overlays (worked in the spike, but untestable and fragile).

Singing synthesis from a score (SoulX-Singer SVS, lyrics + MIDI) is **not** part of 010: it needs
aligned note metadata (a MIDI editor or ASR + note transcription with extra models of up to 4 GB)
and supports only Mandarin, English and Cantonese. Backlog item.

### D3 Consent model for voice profiles

- A voice profile is created only with: a ticked consent statement (versioned text
  `consent.v1`), the typed name of the person (1-80 characters), and a sample of 10-60 s.
- Stored with the profile: statement version, statement text hash, name, timestamp (UTC), source
  (`recorded` | `uploaded`), sample SHA-256 (of the decoded PCM), duration, voiced ratio.
- Sources: browser microphone recording (default) or file upload. No UI path offers tracks, stems,
  takes or exports as a sample; the API rejects an upload whose file-byte SHA-256 equals any
  stored stem, take, export or upload (exact-copy guard; documented as not a similarity check).
- The singer service checks the voiced ratio (RMVPE F0 > 0 on >= 50 % of frames, on the CPU) and returns `422`
  otherwise; the API stores the profile only after this check.
- Deletion removes the sample, its cached F0 and any temporary files; the user chooses whether
  converted takes are deleted; kept takes stay labelled "converted to <name> (profile deleted)".
- Profiles and samples never leave the machine; the singer receives the sample per job and
  deletes its job copy when the job ends.

### D4 Labels for AI output

Takes carry `vocal` = `none | synthetic | converted` (+ profile id/name). File metadata adds
`AI_VOICE=synthetic|converted` and `AI_VOICE_PROFILE=<name>` to the ADR-018 D10 tags. Library and
track badges show "AI-generated" and "AI voice" / "AI voice: <name>".

### D5 No artist or other-person imitation

The ADR-018 D8 guard applies to every free text that reaches the model caption (style
descriptions, free caption, vocal description). Lyrics are the user's own text and are not
filtered for names (a lyric may legitimately mention a name); they never become the caption.
There is no "voice of <artist>" option and no way to upload a voice "model".

### D6 GPU needs and song profiles

Measured (research.md S-04/S-05, NVML above idle + 1 GiB margin): song **planned** (DiT + 1.7B LM,
pt backend) 15.7 GB, 24 s for 60 s / 43 s for 180 s; song **direct** (DiT only, no LM planner)
9.6 GB, 3-5 s; song **direct low-memory** (offload) 7.4 GB, 15-16 s; SVC (fp16) 6.0 GB, 12 s for a
60 s vocal. The gate picks the best profile that fits (ADR-018 D4). The song step and the
conversion step are separate lease holds; the Demucs separation in between runs in the worker
under the same lease.

## Consequences

- A fourth container (`singer`, about 7-8 GB image: torch 2.10 + CUDA wheels, no ACE-Step) from the
  same code base; the 010 weights add about 3.3 GB (SVC 2.8 GB, RMVPE 0.18 GB, whisper-base
  0.29 GB). Voice features show "not installed" when only the generator runs.
- "Use my voice" costs a separation run plus a conversion (research.md S-05/S-06 timings).
- Consent is a stored, versioned record; changing the consent text means `consent.v2` and
  re-consent for new profiles only.

## Alternatives rejected

| Alternative | Why not |
|---|---|
| Copy VoiceStudio code | AGPL-3.0 would make StemStudio AGPL; and it has no singing code |
| Run VoiceStudio as a separate AGPL service | its TTS cannot sing a melody; its RVC hook needs per-user trained models; default weights CC BY-NC |
| YuE2 for lyrics-to-song | 24 GB VRAM default, CC BY-NC weights (CTO decision) |
| SoulX-Singer SVS (score-driven) now | needs aligned score/MIDI editing and extra preprocessing models; 3 languages; SVC covers "my voice" |
| RVC voice models | requires training a per-user model; licences of community models unclear |
| Voice cloning of any uploaded voice | consent cannot be verified for other people; only own-voice profiles with stored consent |

## Phase 0 outcome (architect, 2026-11-15, combined 009 + 010 skeleton)

Contracts: `specs/010-lyrics-to-song/contracts/` generator-api **1.1.0**, backend-api **1.9.0**,
ui-test-ids **1.9.0**, each the 009 document plus additions (subset rules tested in Python and .NET).

| Topic | Decision |
|---|---|
| SoulX-Singer packaging | **Path install in the singer image, no build shim.** Upstream has no packaging; the `soulx` extra of `generator/pyproject.toml` holds only the runtime dependencies of the SVC path (torch/torchaudio `2.10.0+cu128`, transformers `4.41.2`, tokenizers `0.19.1`, accelerate `1.11.0`, librosa `0.11.0`, omegaconf `2.3.0`, scipy, tqdm). The image fetches the source at commit `81aeb3ae772c70093c3de74dc23c92d983801ae4` (git fetch of that commit; the commit hash is the integrity check), copies `soulxsinger/` and `preprocess/tools/f0_extraction.py` (self-contained RMVPE) to `/opt/soulx-singer` and puts it on `PYTHONPATH`. The adapter loads `f0_extraction.py` by file path, because `preprocess/tools/__init__.py` imports the heavy preprocessing tools. Nothing of SoulX is vendored in the repository; nothing AGPL is used. |
| `SONG_PLANNED` re-measure | The language model already runs in **bf16** with ACE-Step's default init; the 15.7 GB of the spike came from its loader (`from_pretrained` in fp32 on the CPU, then `.to(cuda).to(bf16)`; the fp32 blocks stay reserved). Loading the LM **directly in bf16** (the engine adapter overrides `LLMHandler._load_pytorch_model` to pass `torch_dtype=bfloat16`, no upstream change): NVML peak 12,347 MiB over 1,539 MiB idle -> **`SONG_PLANNED` = 12,288 MiB** (11,832 rounded up); 60 s song 13.3 s cold / 11.4 s warm (pt backend, `thinking=True`). Without the override the load peak needs 15,360 MiB. `SONG_DIRECT` 9,830, `SONG_DIRECT_LOWMEM` 7,578, `SVC_FP16` 6,144 MiB unchanged. |
| Two services | One document, two deployments: `/health.kinds` + `voiceSampleCheck`; a kind not served -> `409 code: kindNotServed`. Settings default by engine (`acestep` -> `restyle,song`; `soulx` -> `voiceConversion`). |
| Names | Backend lyrics-sheet operations are `listLyricsSheets`, `getLyricsSheet`, `createLyricsSheet`, `uploadLyricsSheet`, `updateLyricsSheet`, `deleteLyricsSheet` (006 already has `getLyrics` for a track's synced lyrics); the DTOs are `LyricsSheetDto`, `LyricsInput`, ... in `StemStudio.Contracts.Lyrics`. |
| Consent | `consent.v1` English text of data-model 3.3 plus the Dutch translation in the contract example `ConsentStatementV1` (CTO accepted the draft; the owner may edit it before release, which then means `consent.v2`). |
| Problem types | Kebab-case `type` values: `consent-required`, `consent-outdated`, `voice-name-taken`, `voice-not-installed`, `voice-unavailable`, `no-vocals`, `too-short`, `too-long`, `unvoiced`, `library-audio`. |