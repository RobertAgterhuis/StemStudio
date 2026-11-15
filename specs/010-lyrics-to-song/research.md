# Research: Own Lyrics to Song (feature 010)

Architect, 2026-11-15. Builds on `docs/research/roadmap-003-011.md` sections 8-9 and on the 009
spike (`specs/009-style-transfer/research.md`, set-up S-00, licences S-01, environment S-02, lease
S-07, clean-up S-09). Same machine, same throwaway ACE-Step environment.

## S-01 Licences (verified 2026-11-15)

| Component | Licence | Source checked | SHA-256 (file used) |
|---|---|---|---|
| ACE-Step 1.5 code + `acestep-5Hz-lm-1.7B` weights | MIT / MIT | 009 research S-01 | `f161689d...cd7db8` (LM) |
| SoulX-Singer code, commit `81aeb3ae772c70093c3de74dc23c92d983801ae4` | **Apache-2.0** | `LICENSE` file; GitHub licence API `Apache-2.0` | - |
| `Soul-AILab/SoulX-Singer` rev `40493ad90286056c7a9095035164434a79daa8c9`, `model-svc.pt` (2.79 GB) | **Apache-2.0** | HF API `cardData.license = apache-2.0`; card front matter; not gated | `38e72d9e15be6eef3cc937daaf81421a7b5b546816786530bb9aa346cee5d43c` |
| `Soul-AILab/SoulX-Singer-Preprocess` rev `83dc50289d22a81b1e9998f5b9e111aef7c1fdcd`, `rmvpe/rmvpe.pt` (0.18 GB) | **Apache-2.0** | HF API | `6d62215f4306e3ca278246188607209f09af3dc77ed4232efdd069798c4ec193` |
| `openai/whisper-base` rev `e37978b90ca9030d5170a5c07aadb050351a65bb` (SoulX content encoder, 0.29 GB safetensors) | **Apache-2.0** | HF API | recorded by T061 from the cache |
| transformers 4.41.2, tokenizers 0.19.1 (singer service only) | Apache-2.0 | PyPI metadata | - |
| VoiceStudio (debpalash/voicestudio) | **AGPL-3.0**; default OmniVoice weights CC BY-NC | roadmap research 9.1/9.2 | **not used** |

SoulX-Singer README disclaimer: "Do not use the model to impersonate individuals without
authorization or to create deceptive audio" - matched by FR-006/007/010. Not used from the SoulX
preprocess pipeline (not installed): mel-band-roformer karaoke/dereverb, Parakeet ASR (CC BY 4.0),
Paraformer, ROSVOT (only needed for score-driven SVS).

## S-02 VoiceStudio: what can be reused

Checked again for the owner's note. Its code cannot sing (singing only in design documents;
`SPIKE-02-singing.md` superseded; its plan selected SoulX-Singer). Reusable **ideas** (no code):
explicit consent before cloning, "only your own voice" messaging, model licence inventory per
engine, VRAM headroom check before generation, single-resident engine. All are reflected in
ADR-019 (consent model) and ADR-018 (GPU gate, residency). Copying code is excluded (AGPL-3.0 would
force StemStudio to AGPL).

## S-03 Pipeline choice

| Pipeline (roadmap 9.4) | Verdict |
|---|---|
| P1 ACE-Step lyrics -> song with AI voice | **US1** (default) |
| P2 P1 -> worker separation -> SoulX-SVC with own-voice sample -> remix | **US3** "Use my voice" |
| P3 score-driven SoulX SVS | backlog (needs score editing, extra preprocess models, 3 languages) |
| P4 VoiceStudio | rejected |

## S-04 Lyrics-to-song: speed and VRAM

Turbo DiT, 8 steps, shift 3.0, batch 1, `thinking=True` (1.7B LM plans metadata and audio codes),
English lyrics with `[verse]/[chorus]/[bridge]` markers, plus one Dutch test.

| Run | LM backend | Wall time | torch reserved peak | NVML used peak (idle ~1.1 GB) |
|---|---|---|---|---|
| DiT load | - | 8-13 s | 7.5 GB | 9.0-10.6 GB |
| LM load | vLLM | 30-31 s | 11.0 GB (KV cache pre-allocated, 0.665 utilisation) | 12.7-13.0 GB |
| LM load | **pt** | 23 s | 14.1 GB | 15.8 GB |
| Song 60 s | vLLM | 6.3-10.3 s | 11.8 GB | 13.6-13.8 GB |
| Song 60 s | **pt** | 24.0 s | 14.1 GB | 15.8 GB |
| Song 180 s | vLLM | **failed**: ACE-Step guard "Insufficient free VRAM: need ~1.4 GB, only 1.1 GB available" | - | 13.3 GB |
| Song 180 s | **pt** | 43.2 s | 11.9 GB | 14.0 GB |
| Song 45 s Dutch (`nl`) | vLLM / pt | 8.4 s / 15.4 s | 11.8 / 11.9 GB | 14.0 / 14.1 GB |
| Unload after songs | vLLM | - | - | torch free afterwards **9.8 GB** (vLLM keeps its memory until process exit) |
| Unload after songs | **pt** | 0.5 s | - | torch free afterwards 14.9 GB (released) |
| LM on the CPU (pt) + DiT offload, song 60 s | pt (CPU) | **aborted after > 10 min** (12 CPU cores busy) | - | - |
| **No LM** (`thinking=False`, BPM/key/time signature given), song 60 s / 180 s | - | **3.2 s / 5.4 s** | 7.5 / 6.9 GB | 9.7 / 9.1 GB |
| No LM + DiT offload (low-memory), song 60 s / 180 s | - | 16.1 s / 15.1 s | 4.8 / 5.3 GB | 7.0 / 7.5 GB |

Output: FLAC 48 kHz stereo 24-bit, exactly 60.000 s. Separation of the 60 s English take with the
worker's `htdemucs_6s` (CPU, in parallel with the GPU spike: 167 s; on the GPU worker about 5-10 s
per 003 measurements): vocal stem -21.8 dBFS (clear lead vocal), drums -22.8, bass -25.3,
guitar -26.7, piano -56.2 (none), other -28.7.

Intelligibility (rough, automated): whisper-base transcript of that vocal stem recalled **93 %** of
the lyric words in order ("Sing it loud, sing it free / Every note is part of me / ... This little
song belongs to me / Paper planes and a borrowed tune / Howling softly to the afternoon ...").
SC-003 remains an owner listening check.

Decisions:

- **LM backend `pt`** (ADR-018 D6): slower than vLLM (24 s vs 6-10 s for 60 s) but it releases its
  memory on unload and handles 3-minute songs; vLLM failed at 180 s on 16 GB and kept 5 GB after
  unload, which would starve the worker's Demucs. SC-001 (60 s <= 90 s warm) is met with pt.
- `GpuNeed` `SONG_TURBO_LM17` = 15.8 - 1.1 + 1.0 = **15.7 GB** worst case measured with pt: this
  only fits an otherwise idle 16 GB card (torch allocated 14 GB after the pt LM load; the LM
  appears to be held in fp32; Phase 0 T003 re-measures with an explicit bf16 dtype).
- Profiles chosen by free memory (ADR-018 D4): (1) **planned** = DiT + LM (best structure,
  15.7 GB); (2) **direct** = DiT without LM, BPM/key/time signature taken from the request or the
  style defaults (120 BPM, 4/4, key left to the model), 9.6 GB; (3) **direct low-memory** = (2)
  with offload, 7.4 GB (+1 GiB margin each). LM on the CPU is rejected (> 10 min). The take details
  show which profile ran ("planned" / "direct"); the quality difference between (1) and (2) was
  not assessed in the spike (owner listening check SC-003 covers the default).
- Dutch lyrics generate without error (`vocal_language="nl"` is in ACE-Step's 50-language list).

## S-05 SoulX-Singer-SVC inside the ACE-Step environment

**Finding: SoulX-SVC does not run with ACE-Step's transformers 4.57.6** -
`TypeError: LlamaAttention.forward() missing 1 required positional argument: 'position_embeddings'`
(SoulX uses `LlamaDecoderLayer` with the 4.41 calling convention). With **transformers 4.41.2**
(+ tokenizers 0.19.1) overlaid for that process only, on the same **torch 2.10.0+cu128**, it works
on sm_120:

| Run (fp16, 32 steps, cfg 3, `auto_shift`) | Wall time | torch reserved peak | NVML used peak (idle ~1.1-1.4 GB) |
|---|---|---|---|
| Load SVC model (698 M params) + RMVPE + whisper-base | 16.5-24 s | 3.3 GB | 5.5 GB |
| Convert 20 s vocal (Hero of the Day vocal stem excerpt), first / second | 15.0 s / **3.7 s** | 3.7 GB | 5.9 GB |
| Convert **60 s** generated vocal stem (4 segments), first / second | 12.8 s / **12.1 s** | 4.1 GB | **6.2 GB** |

Prompt in the spike: SoulX's own demo prompt `example/audio/en_prompt.mp3` (no real person's voice
was cloned; no owner voice was available). Auto shift chose +3 and -2 semitones. whisper-base
transcript of the converted 60 s vocal recalled **71 %** of the lyric words (93 % before conversion):
words survive, intelligibility drops somewhat (prompt voice and register change). Download sizes:
`model-svc.pt` 2.79 GB, `rmvpe.pt` 0.18 GB, whisper-base 0.28 GB (fetched by SoulX through
`from_pretrained("openai/whisper-base")`; the service must pin the revision and use the model
cache, T003); SoulX + preprocess downloads took 117 s.

Decision (ADR-019 D2, revised): **a second service `singer`** built from the same
`stemstudio_generator` code base with its own lock (torch 2.10.0+cu128, transformers 4.41.2,
librosa, omegaconf; no ACE-Step), exposing the same generator contract for the kinds it supports
(`voiceConversion`, voice-sample checks). Patching SoulX for transformers 4.57 was rejected
(modifying upstream model code, fragile); a per-process `PYTHONPATH` overlay inside one image was
rejected as a production design (two transformers on one path, untestable). `GpuNeed SVC_FP16` =
6.2 - 1.2 + 1.0 = **6.0 GB**.

## S-06 End-to-end "Use my voice" estimate (60 s song)

| Step | Measured / estimated |
|---|---|
| Song 60 s (planned profile, pt LM, warm) | 24 s measured (cold + LM load about 35 s more) |
| Import + worker separation of the take (GPU worker, `htdemucs_6s`) | about 5-10 s estimated from 003 (CPU in the spike: 167 s) |
| SVC 60 s (warm / cold incl. load) | 12 s / about 30 s measured |
| Remix + FLAC + library | < 2 s estimated |
| **Total "Use my voice" after the song exists** | **about 20-45 s** (SC-002 <= 2 min met) |

Each step holds the GPU lease separately, so the worker's separation and the singer's conversion
never overlap on the GPU.

## R-01 Consent model

ADR-019 D3. Versioned statement, typed name, timestamp, sample hash; recording in-app by default;
exact-copy guard on uploads; deletion verified by a disk walk (SC-006). No spoken consent phrase
(would need ASR verification and adds little over the typed name for a single local user).

## R-02 Lyrics handling

ACE-Step accepts lyrics up to 4,096 characters with bracketed section markers (`docs/en/INFERENCE.md`),
`[Instrumental]` for no vocals, and 50 languages (`acestep/constants.py` `VALID_LANGUAGES`, incl.
`nl`). LRC timestamps are stripped (no time alignment is passed to the model).

## Open questions (non-blocking)

| # | Question | Default |
|---|---|---|
| 1 | Consent statement wording EN/NL (data-model 3.3) | as drafted; CTO/owner may edit before T001 |
| 2 | Score-driven singing (SoulX SVS) later? | backlog item after 010 |
