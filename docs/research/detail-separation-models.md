# Research: detail (second-stage) separation models and their licences

Status: research only, no code changed. Date: 2026-11-15.
Scope: a hierarchical second separation stage after Demucs `htdemucs_6s` (vocals, drums, bass,
guitar, piano, other) in the Python 3.12 / PyTorch 2.15 CPU worker. This document checks which
models pass constitution IV: OSI-approved code licence, plus an explicit weight licence that allows
local single-user use and ideally redistribution in a Docker image.

Every licence claim below was checked against the primary source: the model card, LICENSE file,
Zenodo record, GitHub release or README. Where only a mirror or third party states a licence, the
row says so. "Not verified" means exactly that.

## 0. TL;DR

| Topic | Finding |
|---|---|
| **First stage itself (blocker)** | The Demucs README says the weight licence is unclear and that `htdemucs_6s` "likely means CC-NC-BY". Our `docs/THIRD-PARTY-LICENSES.md` entry ("Released with the MIT-licensed Demucs repository") is contradicted by upstream. See section 1. |
| Runtime library | `python-audio-separator` (MIT) works with torch 2.15 but **hard-depends on `diffq`, which is CC BY-NC 4.0**. That fails constitution IV as a dependency. Recommended: run models with a thin in-house runner (onnxruntime plus vendored MIT architecture code), not the package. |
| drums -> kick/snare/toms/cymbals | **No model has an explicit, author-stated weight licence.** Best candidate: inagoy DrumSep (MIT repo; weights hosted outside the repo, licence not confirmed by the author). It runs on the Demucs we already ship. Action: ask the author for written confirmation. |
| vocals -> lead/backing | **Passes:** UVR MDX-Net KARA 2 (MIT per the UVR authors, 52 MB ONNX). Also passes: Musetric Duality/KARA 2 student (Apache-2.0, clean data lineage) and anvuew karaoke BS-RoFormer (GPL-3.0, copyleft). |
| other -> winds | **Conditional pass:** UVR `17_HP-Wind_Inst-UVR` (MIT per UVR's statement; no published SDR). |
| other -> strings, synth/keys; guitar split; piano split | **No acceptable model found.** The only broad model (MVSep Mega 53-stem, BS-RoFormer) has no weight-specific licence statement. Banquet is CC BY-NC-SA. |
| bass | Not splittable in practice; no candidates. |
| Auto-decide | Energy gating on parent stems and sub-stems (no new model). PANNs CNN10 (MIT code, CC BY 4.0 weights, 25 MB) is optional, as a cheap "is there any wind/string/synth?" pre-gate for `other`. |

## 1. Blocker found while checking: the licence of the `htdemucs_6s` weights

Verbatim from https://github.com/adefossez/demucs#license (README, Demucs 4.1.0):

> Demucs code is released under the MIT license as found in the LICENSE file.
> The license of the weights is not clear: MusDB contains some data that were only usable for research purpose.
> I sadly cannot help granting a license, both coming from Meta custom data, or from the original MusDB data.
> The 6 source variant of htdemucs was trained only on Meta internal dataset, which likely means CC-NC-BY license
> is applicable, as it was the case for all models released by FAIR.

Under the rule set for this research ("No licence stated = not acceptable", "CC BY-NC = not
acceptable"), the current first-stage weights fail too. This needs a decision by the project owner,
outside this research. Options:

1. Record a documented exception: local, single-user, non-commercial use.
2. Replace the first stage.

Any detail stage inherits this question. `docs/THIRD-PARTY-LICENSES.md` line 29 should be
corrected either way.

## 2. Runtime and library options

| Option | Code licence | Torch 2.15 CPU / Py 3.12 | How models load | Offline | Verdict |
|---|---|---|---|---|---|
| **python-audio-separator** 0.48.0 (nomadkaraoke) | MIT ([pyproject](https://github.com/nomadkaraoke/python-audio-separator/blob/main/pyproject.toml), [README#license](https://github.com/nomadkaraoke/python-audio-separator#license-)) | `[project]` deps: `torch>=2.3,<3` (non-macOS-arm64), `requires-python >=3.10,!=3.14.1`, so torch 2.15 and Py 3.12 resolve. The `[tool.poetry]` section caps torch `<2.9` on Windows for Py<3.14, but that only applies to Poetry, not uv. | Downloads on first use into `model_file_dir` / `AUDIO_SEPARATOR_MODEL_DIR` (default `/tmp/audio-separator-models/`). Order: the project's `model-configs` GitHub release, then the TRvlvr UVR repo or Meta CDN. Files are checked against SHA-256 in `model-checksums.json`. | Yes, if pre-populated. From reading the code (`separator.py`, `download_file_if_not_exists`), every fetch is skipped when the file exists: the model, `download_checks.json`, and the VR/MDX `model_data_new.json`. `--download_model_only` can seed a Docker layer. **Not tested.** | **Fails IV as a dependency:** hard dep `diffq` (Windows: `diffq-fixed`) is **CC BY-NC 4.0** ([diffq LICENSE](https://github.com/facebookresearch/diffq/blob/main/LICENSE), [PyPI](https://pypi.org/project/diffq/)). It also pins `samplerate==0.1.0` (2017) and `beartype<0.19`. Useful as a **reference implementation**: its MIT code can be vendored without diffq. |
| **ZFTurbo Music-Source-Separation-Training** (MSST) | MIT ([LICENSE](https://github.com/ZFTurbo/Music-Source-Separation-Training/blob/main/LICENSE)) | `requirements.txt` pins `demucs==4.0.0` (conflicts with our 4.1.0), needs `torchaudio` (which we dropped), plus wandb, wxpython, pyaudio and similar training/GUI deps. `torch>=2.6`. | Manual: pass `--config_path` and `--start_check_point`. | Yes (local files). | Do not install as a package. **Vendor the model classes** (`models/bs_roformer`, `mdx23c_tfc_tdf_v3`, `scnet`) and `demix()`, all MIT. |
| **Demucs 4.1.0** (already in the worker) | MIT | Already locked. | `demucs.api` with a local repo dir / `.th` file. | Yes. | Can run the HDemucs-based **inagoy DrumSep** checkpoint with zero new deps. Risk: an old pickled `.th` may need `weights_only=False` under torch>=2.6. **Not tested.** |
| **onnxruntime** (CPU) | MIT ([repo](https://github.com/microsoft/onnxruntime)) | Py 3.12 wheels. | Local `.onnx`. | Yes. | Smallest way to run UVR MDX-Net KARA 2 (STFT in numpy/torch, as audio-separator does). |
| **musetric/lab** `LeadBackingRoformer` | MIT ([repo](https://github.com/musetric/lab)) | torch + safetensors. | Local safetensors. | Yes. | Only needed for the Musetric student model. |
| **stems.cpp** (C++/ggml) | `NOASSERTION` on GitHub (not verified) | n/a | GGUF | Yes | Out of scope (non-Python, licence unverified). |

## 3. Candidate models per split

Legend for **Weight licence status**:

- **Explicit**: the author states a licence for the weights.
- **Implicit**: the weights are published inside an OSI-licensed repo or release, with no
  weight-specific statement.
- **Mirror-only**: a third-party re-uploader asserts the licence.
- **None**: no licence stated.
- **NC**: non-commercial.

CPU runtime is an **order-of-magnitude estimate, relative to the current `htdemucs_6s` pass on the
same song (= 1x)**, inferred from architecture and size. **Nothing was measured.**

### 3.1 drums -> kick, snare, toms, hi-hat, cymbals

| Model | Stems | Weight licence (verbatim + URL) | Status | Code | Size | Reported quality | CPU est. | Hosted |
|---|---|---|---|---|---|---|---|---|
| **DrumSep (inagoy)**, HDemucs | kick, snare, cymbals, toms | The repo has `LICENSE` "MIT License / Copyright (c) 2024 Iñaki Goyeneche" ([LICENSE](https://github.com/inagoy/drumsep/blob/main/LICENSE)), added 2024-10-09, after the weights were published in 2023. The weights are **not in the repo**; `drumsepInstall` fetches them with `gdown 1-Dm666ScPkg8Gt2-lK3Ua0xOudWHZBGC` (Google Drive). The README states no weight licence. In open issue [#9](https://github.com/inagoy/drumsep/issues/9) ("Consulta sobre licencia de los pesos para uso comercial") the author moved the licence question to private contact without stating terms. | Implicit / unconfirmed | MIT | 167 MB (`model_drumsep.th`, 167,400,043 B) | DrumSep test SDR: kick 10.52, snare 6.05, toms 4.68, hh-cymbals 5.03 ([MSST pretrained_models.md](https://github.com/ZFTurbo/Music-Source-Separation-Training/blob/main/docs/pretrained_models.md)) | ~0.3-0.7x (single HDemucs on the drum stem only) | Google Drive (author); re-hosted at [ZFTurbo release v1.0.5](https://github.com/ZFTurbo/Music-Source-Separation-Training/releases/tag/v1.0.5) |
| DrumSep v2 (Gridshift fine-tune of inagoy) | kick, snare, cymbals, toms | Card: "License: MIT, inherited from `inagoy/drumsep`." ([gridshiftstudio/drumsep-onnx](https://huggingface.co/gridshiftstudio/drumsep-onnx)) | Explicit for the fine-tune, but it inherits the v1 question | MIT | 335 MB ONNX, fixed 40 s window (the card cites 4-6 GB RAM for a 40 s window) | Held-out *synthetic* loops, median SDR v1 -> v2: kick 16.5 -> 25.1, snare 3.6 -> 13.0, cymbals 0.9 -> 6.8 (same card). Tuned for electronic drums. | ~0.5-1x | Hugging Face. Int8/fp16 re-exports: [Audante](https://huggingface.co/Audante/drumsep-v2-hdemucs-int8), [b33zy](https://huggingface.co/b33zy/drumsep-v2-onnx) (mirror-only) |
| DrumSep MDX23C 6-stem (aufr33 & jarredou) | kick, snare, toms, hh, ride, crash | No licence upstream. "No license is stated for these weights anywhere upstream ... its original release page is no longer online" ([thepatch/mdx23c-drumsep-GGUF](https://huggingface.co/thepatch/mdx23c-drumsep-GGUF)). `github.com/jarredou/models` and the user `jarredou` now return 404 (checked via GitHub API). Intel's mirror of the 5-stem variant declares `cc-by-nc-sa-4.0` ([Intel/drumsep_mdx23c_jarredou_openvino](https://huggingface.co/Intel/drumsep_mdx23c_jarredou_openvino)). | None (NC per mirror) | MIT (MSST) | 437 MB | kick 14.54, snare 9.79, toms 10.63, hh 3.19, cymbals 6.08 (MSST doc) | ~1-2x | Mirrors only (audio-separator `model-configs`) |
| DrumSep MDX23C 5-stem (jarredou) | kick, snare, toms, hh, cymbals | Same as above; the upstream file is gone ([mdxnet-infer](https://pypi.org/project/mdxnet-infer/): "No LICENSE file or explicit terms accompanied the original GitHub Release") | None / NC (Intel) | MIT | ~220 MB | kick 16.66, snare 11.53, toms 12.33, hh 4.04, cymbals 6.36 | ~1-2x | Gone upstream |
| LarsNet (polimi-ispl) | kick, snare, toms, hihat, cymbals | "Pretrained LarsNet model checkpoints ... licensed under CC BY-NC 4.0." ([README](https://github.com/polimi-ispl/larsnet#readme)) | **NC, rejected** | repo licence not set on GitHub | 5 x 118 MB = 590 MB | (not checked) | ~0.5x | Google Drive / HF |
| drumsep-ht-roformer (Sucial) | kick, snare, hihat, tom, cymbal, other | None on the card ([HF](https://huggingface.co/Sucial/drumsep-ht-roformer)) | None, rejected | - | 747 MB | - | ~2-4x | HF |
| MVSep Mega 53-stem (BS-RoFormer) | includes kick, snare, toms, hh | See 3.3 | Implicit | MIT | 1.37 GB | - | very high | GitHub release |

### 3.2 vocals -> lead vocals, backing vocals

| Model | Stems | Weight licence (verbatim + URL) | Status | Code | Size | Reported quality | CPU est. | Hosted |
|---|---|---|---|---|---|---|---|---|
| **UVR MDX-Net KARA 2** (`UVR_MDXNET_KARA_2.onnx`) | lead vocal / rest (backing + instrumental) | UVR README: "UVR's core developers trained all of the models provided in this package (except for the Demucs v3 and v4 4-stem models)" and "For all third-party application developers who wish to use our models, please honor the MIT license by providing credit to UVR and its developers." ([README](https://github.com/Anjok07/ultimatevocalremovergui#license), repo licence MIT) | Explicit (in prose; requires credit) | MIT | 52 MB | audio-separator benchmark on full mixes, MUSDB "vocals" target: median vocals SDR 5.43 ([models-scores.json](https://github.com/nomadkaraoke/python-audio-separator/blob/main/audio_separator/models-scores.json)). Not a lead/backing metric. | ~0.2-0.5x | [TRvlvr/model_repo release](https://github.com/TRvlvr/model_repo/releases/tag/all_public_uvr_models) (that repo has no LICENSE file; the licence comes from the UVR statement) |
| **Musetric Duality + KARA 2 student** (Mel-Band RoFormer) | instrumental, lead, backing (one pass) | Card front matter `license: apache-2.0`. Lineage: Duality Apache-2.0, then SYH MIT, then Kim MIT; split teacher UVR KARA 2 MIT. Training data: "No NonCommercial, NoDerivatives or ShareAlike material"; every track is listed in `training-data.csv` ([card](https://huggingface.co/musetric/mel-band-roformer-duality-kara2-student)) | **Explicit, cleanest lineage found** | MIT (musetric/lab) | 464 MB | ccMixter true stems, median SDR on mixes: lead 6.20, backing 1.90; on a cappellas: lead 3.65, backing 0.49. Card: "it separates the backing weakly". | ~2-4x (116 M params, 11 s windows) | Hugging Face |
| anvuew karaoke BS-RoFormer | Vocals (lead) / Instrumental | Card front matter `license: gpl-3.0` ("dataset by becruily") ([card](https://huggingface.co/anvuew/karaoke_bs_roformer)) | Explicit, **copyleft**: Docker redistribution must ship the GPL text and offer the "source" | MIT (MSST) | 204 MB | None published | ~2-3x | Hugging Face |
| UVR-BVE-4B_SN-44100-1/2 (VR arch) | backing vocals extractor | Covered by the UVR statement above, *if* UVR-team trained (inferred from the name; not separately confirmed) | Explicit (conditional) | MIT | 223 MB | audio-separator benchmark not meaningful for BVE (median vocals SDR 0.04) | ~0.5-1x | TRvlvr release |
| 5_HP / 6_HP-Karaoke-UVR (VR arch) | lead / rest | UVR statement | Explicit | MIT | 126 MB | median vocals SDR 4.64 (6_HP) on the same benchmark | ~0.3-0.7x | TRvlvr release |
| Mel-RoFormer Karaoke aufr33-viperx | lead / rest | In UVR's public model release, but co-trained by viperx (not a UVR core developer); no card licence | Implicit / ambiguous | MIT | 913 MB | median vocals SDR 8.45 on the same benchmark (best of the checked models) | ~3-5x | TRvlvr release |
| becruily Mel/BS karaoke, Gabox karaoke, Gonzaluigi BVE | lead / rest, BVE | No licence on the HF repos ([becruily mel](https://huggingface.co/becruily/mel-band-roformer-karaoke), [becruily bs](https://huggingface.co/becruily/bs-roformer-karaoke), [GaboxR67](https://huggingface.co/GaboxR67/MelBandRoformers), [Gonzaluigi](https://huggingface.co/Gonzaluigi/Mel-Band-Roformer-BVE-Gonzaluigi)) | None, rejected | - | 204 MB - 1.7 GB | - | 2-6x | HF |
| Sucial Chorus Male-Female BS-RoFormer | male / female | `cc-by-nc-sa-4.0` ([card](https://huggingface.co/Sucial/Chorus_Male_Female_BS_Roformer)) | **NC, rejected** | - | 527 MB | - | ~3x | HF |
| MedleyVox (Cyru5 weights) | multiple singers | Not verified (the WebUI repo is MIT; the weights' own licence was not found) | Not verified | - | ~233 MB + 3.8 GB XLSR | - | high | HF |

### 3.3 other -> strings, winds/brass, synth/keys (and guitar / piano splits)

| Model | Stems | Weight licence (verbatim + URL) | Status | Code | Size | Reported quality | CPU est. | Hosted |
|---|---|---|---|---|---|---|---|---|
| **UVR 17_HP-Wind_Inst-UVR** (VR arch) | woodwinds / no woodwinds (wind instruments vs rest) | UVR statement (see 3.2); UVR-team authorship inferred from the `-UVR` suffix | Explicit (conditional) | MIT | 223 MB | No published scores (`median_scores: {}` in models-scores.json) | ~0.5-1x | TRvlvr release |
| MVSep Mega 53-stem (ZFTurbo, BS-RoFormer) | 53, incl. strings, bowed_strings, violin, cello, brass, trumpet, woodwind, sax, flute, synth, keys, organ, piano, digital-piano, acoustic-guitar, electric-guitar, lead-vocal, back-vocal, kick, snare, toms, hh | The release notes contain no licence text ([release v1.0.21](https://github.com/ZFTurbo/Music-Source-Separation-Training/releases/tag/v1.0.21)); the repo is MIT; the training data is undisclosed. Release note: "Currently, the model's performance on individual stems may be lower than the specialized models available on the MVSep site." | Implicit, fails the strict rule | MIT | 1.37 GB (single ckpt); the "16 GB of VRAM" advice signals very high cost | No SDR published | **very high** (estimate 10x+ on CPU for all stems) | GitHub release; split per-stem re-host by [noblebarkrr](https://huggingface.co/noblebarkrr/BS-Roformer-MVSep-Mega-53-stems) (no licence) |
| Banquet (query-bandit, query-based) | any queried instrument (guitar, piano, reeds, organ, ...) | Zenodo record 13694558: `cc-by-nc-sa-4.0` ([Zenodo](https://zenodo.org/records/13694558)) | **NC, rejected** | MIT ([repo](https://github.com/kwatcharasupat/query-bandit)) | 550-770 MB | outperformed htdemucs_6s on guitar and piano on MoisesDB (paper abstract) | ~1-3x per query | Zenodo |
| becruily guitar Mel-RoFormer | guitar | No licence ([HF](https://huggingface.co/becruily/mel-band-roformer-guitar)) | None, rejected | - | 45 MB | - | ~1x | HF |
| SCNet (starrytong / ZFTurbo releases) | bass, drums, vocals, other only | MIT repo, weights implicit | n/a (no detail stems) | MIT | 42-216 MB | MUSDB 9.0-10.1 avg | - | GitHub |

No model with an acceptable licence separates **strings**, **synth/keys**, **acoustic vs electric
guitar** or **piano sub-parts**. **Bass**: no sub-stem models were found and none are needed.

## 4. Verdict per split

| Split | Best model that passes IV | Licence | Notes |
|---|---|---|---|
| drums -> kick / snare / toms / cymbals | **No acceptable model found** under the strict rule. Candidate if the author confirms: **inagoy DrumSep** (HDemucs, 167 MB). | MIT repo; weights unconfirmed | Runs on the existing Demucs. Ask in an issue on `inagoy/drumsep` whether `modelo_final.th` is covered by the MIT LICENSE. If yes, also consider the Gridshift v2 fine-tune (MIT) for electronic drums. Hi-hat stays merged into "cymbals". |
| vocals -> lead / backing | **UVR MDX-Net KARA 2** | MIT (UVR statement; credit required) | 52 MB ONNX, cheapest. Evaluate the Musetric student (Apache-2.0) as a quality alternative; avoid GPL anvuew unless copyleft is accepted. Backing quality is weak for all of them. |
| other -> winds / brass | **UVR 17_HP-Wind_Inst-UVR** (conditional on UVR authorship) | MIT (UVR statement) | No published metrics. Brass and woodwind are not separated from each other. |
| other -> strings | **No acceptable model found** | - | Only the implicit 53-stem model or NC Banquet. |
| other -> synth / keys | **No acceptable model found** | - | Same. |
| guitar -> acoustic / electric | **No acceptable model found** | - | Same. |
| piano -> sub-parts | **No acceptable model found** | - | Same. |
| bass | Not applicable | - | Do not attempt. |

## 5. Automatically deciding what to split

| Approach | Licence | Cost | Pros | Cons |
|---|---|---|---|---|
| **A. Energy gating** (RMS/loudness on parent and sub-stems) | none (numpy/librosa, already locked) | ~0 | No new model or download; deterministic; testable with fixtures; directly measures what the user hears | Cannot avoid running an expensive model on a stem that has energy but lacks the target instrument (e.g. `other` without winds) |
| B. PANNs AudioSet tagger on `other` | code MIT ([audioset_tagging_cnn](https://github.com/qiuqiangkong/audioset_tagging_cnn), [panns_inference](https://github.com/qiuqiangkong/panns_inference)); weights **CC BY 4.0** ([Zenodo 3987831](https://zenodo.org/records/3987831)) | CNN10 25 MB / MobileNetV1 24 MB / CNN14 327 MB; well under 1x on CPU (estimate) | Relevant AudioSet classes exist: String section, Violin, Cello, Bowed string instrument, Orchestra, Brass instrument, Trumpet, Trombone, French horn, Wind instrument/woodwind, Flute, Saxophone, Clarinet, Synthesizer, Organ, Electric piano, Electric/Acoustic guitar, Choir, Snare drum, Hi-hat, Cymbal ([class list](https://github.com/qiuqiangkong/audioset_tagging_cnn/blob/master/metadata/class_labels_indices.csv)) | Trained on full mixes, not stems; thresholds need tuning; CC BY needs attribution |
| C. AST (MIT/ast-finetuned-audioset) | code BSD-3 ([YuanGongND/ast](https://github.com/YuanGongND/ast)); HF weights card `bsd-3-clause` ([HF](https://huggingface.co/MIT/ast-finetuned-audioset-10-10-0.4593)) | ~350 MB, heavier than CNN10 | Higher tagging accuracy | Needs `transformers`; heavier |
| D. LAION CLAP zero-shot | code CC0 ([LAION-AI/CLAP](https://github.com/LAION-AI/CLAP)); weights `apache-2.0` ([laion/larger_clap_music](https://huggingface.co/laion/larger_clap_music)) | ~600 MB+ | Free-text queries ("string section") | Heaviest; less calibrated |
| Essentia models (MTG) | "licensed under CC BY-NC-SA 4.0" ([essentia.upf.edu/models.html](https://essentia.upf.edu/models.html)) | - | - | NC, rejected |

**Recommendation: A, with B as an optional pre-gate for `other` only.**

1. **Parent gate (before any detail model).** For each first-stage stem compute frame RMS (e.g.
   2048/512) and the share of "active" frames above -50 dBFS. Skip the detail split if the stem's
   integrated level is below about -45 dBFS, or if fewer than about 5 % of frames are active. This
   removes the silent and bleed-only stems that htdemucs_6s always emits.
2. **Sub-stem gate (after the detail model).** Keep a sub-stem only if its energy relative to the
   parent is above about -20 dB (1 % of the parent's energy) *and* its active-frame share passes. Merge
   discarded sub-stems back into a residual ("drums other" / "other rest") so the sub-stems still
   sum to the parent.
3. **Sanity check.** If the sum of the kept sub-stems deviates from the parent by more than a
   tolerance (residual energy above about -15 dB relative to the parent), keep the parent unsplit. A
   detail model applied out of domain fails in exactly this way.
4. **Optional (B).** Run PANNs CNN10 on a handful of 10 s windows of `other`, and run the wind model
   only if the max of {Wind instrument, Brass instrument, Flute, Saxophone, Trumpet, ...} exceeds a
   tuned threshold. This is worth adding only because the `other` models are expensive and the
   instrument is often absent. For drums and vocals the parent gate (1) is enough: almost every song
   with drums has a kick and a snare.

Rationale: A needs no new dependency or licence and is fully unit-testable. It also answers the
question the user actually cares about: is there audible content in this sub-stem? A tagger can
only predict presence. All thresholds are starting points to tune on the QA fixtures.

## 6. Risks

| Risk | Detail | Mitigation |
|---|---|---|
| Licence (first stage) | `htdemucs_6s` weights are "likely CC-NC-BY" per upstream (section 1) | Owner decision; correct THIRD-PARTY-LICENSES |
| Licence (detail) | Most community models (MVSep/UVR community, HF) state no licence; mirrors assert MIT or NC inconsistently; upstreams disappear (jarredou) | Accept only the "Explicit" rows; pin SHA-256; keep the evidence URLs in THIRD-PARTY-LICENSES; credit UVR and PANNs as required |
| Copyleft and NC deps | `audio-separator` pulls in `diffq` (CC BY-NC 4.0); the anvuew model is GPL-3.0 | Vendor MIT code instead of the package; avoid GPL weights in the image unless accepted |
| Download / image size | KARA 2 52 MB + wind 223 MB + DrumSep 167 MB is about 0.45 GB. The Musetric student adds 464 MB; the 53-stem model alone is 1.37 GB. | Bake only the selected models into a separate image layer, or a volume seeded at first run |
| CPU time multiplication | Each detail model is another full pass over (part of) the song. Estimates relative to htdemucs_6s: KARA 2 ~0.2-0.5x, DrumSep ~0.3-0.7x, VR wind ~0.5-1x, RoFormer karaoke 2-5x, 53-stem 10x+. **Not measured.** | Gate first (section 5); run detail splits as a separate, optional job phase with its own progress; benchmark on the reference CPU before choosing RoFormer models |
| Quality / domain | Detail models are trained on clean or synthetic stems (inagoy: 7 h of sample-based drums per [issue #1](https://github.com/inagoy/drumsep/issues/1); Gridshift v2: synthetic loops). On htdemucs output they get bleed; inagoy toms are weak (SDR 4.68; issue #9 reports toms catching kick energy); hi-hat is not separate in the 4-stem DrumSep; lead/backing backing-SDR is 0.5-1.9 dB; the wind model is unscored | Present detail stems as "experimental"; keep the parent stem available; residual stem so nothing is lost |
| Compatibility | Old pickled `.th` (DrumSep) under torch 2.15 `weights_only` default; VR-arch models need UVR's `model_data_new.json` parameters (from `TRvlvr/application_data`, licence not checked) | Integration test per model; vendor the parameter entries with the model |
| Memory | RoFormer models on CPU need several GB of RAM for long chunks; the DrumSep v2 ONNX has a fixed 40 s window (the card cites 4-6 GB) | Prefer the PyTorch HDemucs path; set chunk sizes explicitly |

## 7. Not verified

- Offline behaviour of python-audio-separator (from reading the code only).
- Actual CPU timings for any model.
- That `17_HP-Wind_Inst-UVR` and `UVR-BVE-4B` were trained by the UVR core developers (inferred from naming plus the UVR README statement).
- The licence of the MedleyVox weights, the stems.cpp repo, and the `TRvlvr/application_data` parameter files.
- Whether inagoy intends the 2024 MIT LICENSE to cover the Google-Drive-hosted weights.
