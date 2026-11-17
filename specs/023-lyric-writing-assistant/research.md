# Research: Lyric Writing Assistant (feature 023)

Architect, 2026-11-16. Decisions are binding through ADR-037. Items marked **measure (Phase 0)**
are not measured yet: no model was run for this design (design-only task, no Docker); the Phase 0
spike T008 measures them on the reference machine and updates this file.

## R-01 Where the assistant runs

| Option | For | Against |
|---|---|---|
| A. A tool-less "assistant" service inside `StemStudio.Agent`, called by dedicated backend endpoints; the agent gets thin tools that call those endpoints | one runtime, one backend selection, the Claude exception applies unchanged (it covers "an alternative LLM backend of the AI composer agent"); the model that reads the sheet has no tools (injection cannot act); results are structured data; the editor works without a conversation | a second prompt family next to the agent loop |
| B. Everything through 011 agent runs (the editor sends a hidden command) | reuses runs, events, approvals | sheet text would enter the agent's model context (conflicts with the 011 rule that sheet text never passes through the agent model); free-text answers instead of anchored suggestions; slower (tool loop) |
| C. A separate service/container | isolation | duplicate backend handling, key handling and GPU rules; more components |

**Decision: A.** `LyricAssistant` in `StemStudio.Agent/LyricAssist/` uses the same
`IChatClient` factories, model selector (default/fallback) and Claude key resolution as the agent
(011 + claude-key), with its own prompts, limits and output schema. Endpoints live in the API
(`/api/lyrics/...`), persistence in `StemStudio.Persistence`.

## R-02 Deterministic layer: spelling

| Candidate | Licence | Footprint | Lyrics fit |
|---|---|---|---|
| **WeCantSpell.Hunspell** 7.0.1 (NuGet, 2025-11; .NET 8/10, .NET Standard 2.0) - managed port of Hunspell | MPL-1.1 / GPL-2.0 / LGPL-2.1 tri-licence (inherited from Hunspell); used under **MPL-1.1** (OSI, file-level copyleft, no effect on StemStudio's MIT code; we do not modify its files) | in-process; dictionaries load in < 1 s (**measure (Phase 0)**), about 30-60 MB RAM for both languages | spelling only; suggestions ranked by edit distance; no grammar |
| LanguageTool 6.x (server) | LGPL-2.1-or-later (OSI); Java 17 | separate JVM service, about 1 GB RAM recommended, image 300+ MB, start-up 10-20 s; optional n-gram data 8+ GB | spelling + grammar + style rules for nl (OpenTaal community rules) and en; many rules flag deliberate lyric language (missing articles, repetition, inversions, informal forms) |
| Hunspell native (libhunspell) via P/Invoke | same tri-licence | native binary per platform | same as the managed port, more build work |

**Dictionaries**

| Data | Source | Licence (verified 2026-11-16) | Notes |
|---|---|---|---|
| Dutch `nl.dic`/`nl.aff` | OpenTaal (`github.com/OpenTaal/opentaal-hunspell`) | dual **BSD-3-Clause** or CC BY 3.0; StemStudio uses BSD-3-Clause | the standard Dutch word list (Woordenlijst-compatible spelling) |
| English `en_US`, `en_GB-ise`/`en_GB-ize` | SCOWL / `wordlist.aspell.net` (`github.com/en-wl/wordlist`) | "MIT-like" combined licence (README; exact text in its `Copyright` file, to be copied verbatim with the files) | accepted when either variant accepts the word |
| CMU Pronouncing Dictionary | `github.com/cmusphinx/cmudict` | BSD-2-Clause-style (CMU wording, no advertising clause) | ~134k words with ARPAbet phones and stress: syllables and rhymes for English |

**Decision:** WeCantSpell.Hunspell + OpenTaal + SCOWL in process, **no LanguageTool in this
feature**. Grammar, flow and devices come from the model; LanguageTool stays a measured follow-up
(owner decision 2026-11-16: no for now; decision point T080 if Dutch grammar recall < 60 %) because (a) its value over the model for Dutch is unknown until SC-001 is
measured, (b) it adds a 1 GB JVM service the owner has not asked for, (c) its rule set needs a
large lyric-specific suppression list to respect FR-009. The port `IGrammarRules` is **not**
created now (YAGNI); adding it later is additive.

Dictionaries are **vendored** under `src/StemStudio.Api/LyricAssist/Dictionaries/` (about 10 MB)
with `PROVENANCE.md` (URL, version or commit, SHA-256, licence file) so the Docker image, the
non-Docker path and CI behave identically and offline. Unit tests use tiny fixture dictionaries;
one test class loads the real ones.

## R-03 Poetic licence: how false positives are avoided

Layered, in this order (each layer can only remove or soften a flag, never add an error):

1. **Never checked**: section markers, blank lines, text inside intentional marks, personal words.
2. **Licence lexicon** (embedded, per language, curated, tested): Dutch elisions and spoken forms
   (`'k`, `'t`, `'n`, `m'n`, `z'n`, `d'r`, `da's`, `'s nachts`, `effe`, `nie`, `ff`, `ik ga d'r
   heen`), dropped final `-n` (`lope`, `make`), archaisms (`gij`, `uw`, `thans`, `weleer`,
   `ende`, `der`); English (`gonna`, `wanna`, `gotta`, `'cause`, `'til`, `ain't`, `y'all`,
   `-in'` endings, `o'er`, `e'en`, `thee`, `thou`, `ere`, `oft`). A lexicon hit is a recognised
   licence: never `error`; reported only at strict as `idea`.
3. **Code-switch**: a word unknown in the sheet language but known in the other supported
   language is a recognised licence (`licence.code-switch`).
4. **Sound spelling**: a dictionary miss whose letters are a repetition-stretched form of a known
   word (`zooooo`, `yeahhh`, `nooo`) or a known interjection (`oh`, `ooh`, `na na`, `lala`,
   `hey`) is a recognised licence.
5. **Model classification**: remaining dictionary misses are passed to the model (when available)
   together with the block; the model classifies each as `typo` (with a correction) or
   `licence.*` (kept). Without a model, a remaining miss is reported as `improvement` (not
   `error`) with the dictionary's suggestions, so loose mode shows nothing doubtful.
6. **Prompt rules for grammar and flow**: the system prompt lists the licence types as allowed
   and requires severity `error` only for mistakes that are not plausible as deliberate in a song
   (agreement errors, `d/t` errors in Dutch verb forms such as `hij word`, wrong homophones such as
   `their/there`, broken sentences that make the line unclear). Inversions, missing articles,
   fragments, repetition and informal grammar are at most `improvement` with a reason.
7. **Sensitivity filter** (deterministic, server side): loose keeps `error`; balanced keeps
   `error` + `improvement`; strict keeps all, including recognised licences as `idea`.

The poetic-licence trap cases of the evaluation set (R-09) measure step 6 honestly.

## R-04 Syllables, rhymes and rhythm

- **Dutch syllables**: rule-based vowel-group counting with the Dutch digraph and trigraph table
  (`aa ee oo uu ie oe eu ui ij ei au ou`, `ieu eeuw oei aai ooi uw`), diaeresis splits (`ruïne`,
  `België`), `-ij`/`-y` handling, elision tokens (`'k` = 0, `'t` = 0 standing alone), and an
  exception list for loanwords (`baby`, `team`, `cake`, `love`). Gold list of 300 words
  (SC-007, >= 97 %).
- **English syllables**: CMUdict vowel phones (stress digits) for in-dictionary words; a rule
  fallback (vowel groups, silent final `e`, `-le`, `-ed` rules) for others. Gold list 300 words
  (>= 95 % overall; in-dictionary 100 % by construction).
- **Rhymes, English**: CMUdict rhyme part = phones from the last stressed vowel; perfect = equal
  rhyme part, near = equal vowel with a different final consonant class, or the same consonants
  with a different vowel (assonance/consonance flags).
- **Rhymes, Dutch**: no open Dutch pronunciation lexicon with a permissive licence was found
  (CELEX is not open; eSpeak NG is GPL-3.0 and would need a separate process); Dutch spelling is
  close to phonemic, so a **rhyme key** is computed from the spelling of the last stressed syllable
  with normalisation (`ei = ij`, `au = ou`, final `d = t`, `g = ch` at the end, `-en` with
  silent `n`, open/closed vowel spelling `aa`/`a`), stressed syllable by default the penultimate
  for words ending in `-e`/`-en`/`-er`/`-el`, else the last, with an exception list for common
  prefixes (`be-`, `ge-`, `ver-`, `ont-` are unstressed). Candidates come from the OpenTaal word
  list. Quality is "good, not perfect" (documented); the model may add rhymes but every shown rhyme
  is accepted only when the rhyme key matches (perfect) or the vowel matches (near).
- **Rhythm target**: target syllables per line = beats per bar x bars per line x syllables per
  beat (rounded). Without a target the reference is the median of the section's lines (sections
  from 010 `LyricsSections.Parse`; without markers, blank-line blocks). Deviation > 2 is marked.
  The 010 density (words per second) is shown for `targetDuration = lines x bars per line x beats
  per bar x 60 / BPM`.

## R-05 Model prompting, output and streaming

- **Blocks**: the sheet is split into blocks of at most 8 lines along section and blank-line
  boundaries; each block is one model request with the whole sheet as read-only context (so flow
  and rhyme across blocks are visible) and the block marked as the part to review. A 40-line sheet
  is about 5 blocks. Blocks run sequentially (one GPU model), results stream per block.
- **Output**: JSON constrained to [contracts/lyric-assist-output.schema.json](contracts/lyric-assist-output.schema.json)
  (Ollama `format` with the JSON schema; Claude: a forced single tool `emit_suggestions` whose
  input schema is the same; no other tools). `think: false` for Ollama (011 F-02 latency).
  Temperature 0.2 for checks, 0.8 for writing help.
- **Validation** (`SuggestionValidator`, pure): schema, anchor (line exists, `original` equals the
  text at `start/length`, or is re-found once on the same line), replacement length (<= 400; a
  replacement may not change more than 60 % of a line for corrections), allowed categories for the
  request kind, sensitivity filter, intentional marks and personal words, rhyme and syllable
  verification (R-04), verbatim check (R-07), duplicate merge (same anchor and replacement).
- **Latency (estimate, measure (Phase 0))**: 011 F-02 measured 0.4-1.1 s per short tool turn warm;
  a block request has about 1,500 prompt tokens and 150-400 output tokens. At an assumed 60-90
  tokens/s for 9b on the RTX 5070 Ti, a block takes 3-7 s, a 40-line sheet 15-35 s. SC-004 (first
  model suggestion <= 6 s, full <= 30 s p50) is set from this estimate and is confirmed or
  adjusted with an ADR note in T008.
- **Context size**: `num_ctx` 16384 as in 011 (F-08: 5.8 GB VRAM); a 4,096-character sheet is
  about 1,300 tokens.

## R-06 Model choice and Dutch quality

`qwen3.5:9b` (Apache-2.0) is multilingual with Dutch in its training data, but Dutch spelling
(`d/t`, `-dt`) and grammar subtleties are a known weakness of models this size; English is
expected to be clearly better. No honest number exists yet. Therefore:

- the evaluation set (R-09) is the gate (SC-001); the dictionary layer guarantees a deterministic
  Dutch spelling baseline that does not depend on the model;
- the pane always shows which layer and model produced a suggestion (FR-005, FR-029);
- if Dutch misses SC-001 locally, the standing quality note is shown and the owner decides on
  LanguageTool (decision point T080, Dutch grammar recall < 60 %); Claude remains the optional quality path (SC-001 Claude targets);
- `qwen3.5:4b` (fallback while a GPU job runs) is measured too and reported separately; it is not
  gated.

## R-07 Copyright: own lyrics only, verbatim detection

- The only lyrics corpus available locally is the 006 track lyrics in the library (LRCLIB
  downloads and user-pasted karaoke text). An **n-gram index** (7-word shingles and normalised full
  lines of 5+ words; lower case, no diacritics, punctuation removed) is built in memory from all
  `TrackLyrics` texts at first use and refreshed when lyrics change (event from the 006 service).
  For 500 tracks x 60 lines this is about 30,000 lines / 300,000 shingles: a few MB.
- **Sheet guard** (before any assistant action): >= 3 matching lines of 5+ words **and** >= 25 % of
  the sheet's non-marker lines -> `not-own-lyrics` with the track title. Thresholds avoid false
  positives on clichés ("I love you baby"); tested with a self-written cliché set.
- **Own-work override** (owner decision Q1): the user may confirm that a refused sheet is their own
  work (e.g. their band's song that was imported); the guard then ignores matches with that one
  track only. The output guard below stays active for every track, including the matched one.
- **Output guard**: any generated text with a matching 7-word shingle or full line is withheld.
- **No online check**: sending the user's text to LRCLIB search would leak it; not done.
- **Limit (documented)**: lyrics that are not in the library cannot be detected; the prompt
  forbids quoting or continuing existing songs, and the Claude backend has its own safeguards.

## R-08 Prompt injection

The sheet text is user data, but a user may paste text they did not write, and 011 treats any
external text as untrusted. Measures: no tools for the assistant model; data delimiters with a
per-request random boundary; the system prompt says the text is a poem to review, never
instructions; output schema only; anchors verified against the real text; explanations rendered as
plain text (Blazor encoding); suggestion texts are capped and never executed; agent results carry
no text (FR-019), so nothing from a sheet reaches the agent loop. Injection cases are in the
evaluation set (L-I1..L-I4) and in the automated tests with an adversarial fake model.

## R-09 Evaluation

`tools/agent-eval` gets a second suite `--suite lyric` reading `tools/agent-eval/lyric-eval-set.json`
(converted from [evaluation/lyric-eval-set.md](evaluation/lyric-eval-set.md) by the ai-agent).
It calls the assistant service directly (not the agent loop) with the real backend and the real
dictionaries, grades per case (expected corrections found at the right anchor with an acceptable
replacement; forbidden flags absent; device claims verified), and prints recall, precision and the
licence false-positive rate per language and category, plus latency percentiles. Runs on the
reference machine only (not CI); CI runs the suite's self-test with a scripted fake model.

## R-10 Licences of new dependencies (for THIRD-PARTY-LICENSES, CTO)

| Name | Version | Licence | URL |
|---|---|---|---|
| WeCantSpell.Hunspell | 7.0.1 (pin re-checked at Phase 0, >= 14 days old) | MPL-1.1 (chosen from MPL/GPL/LGPL tri-licence) | https://github.com/aarondandy/WeCantSpell.Hunspell |
| OpenTaal Hunspell nl | pinned release at Phase 0 | BSD-3-Clause (chosen from BSD-3-Clause / CC BY 3.0) | https://github.com/OpenTaal/opentaal-hunspell |
| SCOWL Hunspell en_US, en_GB-ise, en_GB-ize | pinned release at Phase 0 | MIT-like (SCOWL `Copyright` file) | https://github.com/en-wl/wordlist |
| CMU Pronouncing Dictionary | pinned commit at Phase 0 | BSD-2-Clause-style (CMU) | https://github.com/cmusphinx/cmudict |

No new model weights; `qwen3.5:9b`/`4b` are already used by 011.

## Phase 0 results (architect, 2026-11-17)

**R-02 / R-10 pinned (T006).** Retrieved 2026-11-17, every release or commit older than 14 days; files
byte-identical to upstream (a nested `.gitattributes` keeps the CRLF of OpenTaal and SCOWL), SHA-256 in
`src/StemStudio.Api/LyricAssist/Dictionaries/{PROVENANCE.md, manifest.json}`, checked by
`VendoredDictionaryTests` (also loads every dictionary with WeCantSpell.Hunspell). Total 7,740,587 bytes
(7.4 MiB, below the ~10 MB estimate and the +15 MB image budget of T069).

| Data / package | Version (pin) | Licence (verified from the upstream file) |
|---|---|---|
| WeCantSpell.Hunspell | 7.0.1 (NuGet, 2025-11-30; net8.0/net10.0/netstandard2.0, no dependencies on net10.0) | MPL-1.1 chosen from MPL-1.1 / GPL-2.0 / LGPL-2.1; used unmodified |
| OpenTaal Hunspell nl | 2.20.23, commit `1c22bc3d` (2022-11-29; the later untagged "intermediate" commit is not used) | BSD-3-Clause chosen from "BSD-3-Clause and/or CC BY 3.0" (`nl/LICENSE.txt`) |
| SCOWL Hunspell en_US, en_GB-ise, en_GB-ize | release `rel-2026.02.25` (SCOWL commit `7e99eda`, size 60; SCOWL-generated, **not** the LibreOffice en_GB) | SCOWL notice (MIT/HPND-style) for the word lists, Kuenning Ispell BSD-style for the affix files (`README_<dict>.txt`) |
| CMU Pronouncing Dictionary | commit `74790861` (2025-10-24), 135,166 lines | BSD-2-Clause wording (`cmudict/LICENSE`) |

None is copyleft beyond MPL's file-level scope; nothing GPL is bundled (WeCantSpell is used under MPL-1.1).

**R-05 / R-06 spike S-01 (T008): not run in Phase 0.** The reference machine was busy with another
integration (at most one heavy process; the owner's Docker stack, which hosts Ollama, is off limits). Per
tasks.md ("T008 may finish during the wave but before T068") the spike moves to the ai-agent lane of the
wave, run against a natively started Ollama only when the machine is free. Until then SC-004 and
`BlockLines = 8` stay the design estimates of R-05. Contract impact if the spike changes them: none
(`BlockLines` is configuration, `LyricAssistLimits.DefaultBlockLines` the default).

**R-05 / R-06 spike S-01 (T008): run in wave 1 (ai-agent, 2026-11-17).** The Ollama was a throwaway container of
this lane only: `ollama/ollama:0.40.13` on its own port and volume, GPU shared; neither the owner's stack nor the host
Ollama was used. It was removed afterwards. Tool: `tools/agent-eval --suite lyric --sheet <file>`, which runs the real
assistant: R-05 prompts with a random boundary, `format` = lyric-assist-output 1.0.0, `think: false`, `num_ctx` 16384.
Blocks are cut at blank lines and at most 8 lines, an approximation of `SheetBlocks`. The sheets are
`p1-havenlicht.nl.txt` (49 lines incl. markers, 10 blocks) and `p2-harbour-light.en.txt` (10 blocks).

| Model / sheet | Runs | First block (s) | Full sheet (s) | Per block median (s) | Valid JSON |
|---|---|---|---|---|---|
| `qwen3.5:9b` GPU, P1 nl, cold | 1 | 53.8 (model load) | 79.2 | 3.3 | 10/11 (one retry succeeded) |
| `qwen3.5:9b` GPU, P1 nl, warm | 3 | 1.5 / 1.2 / 5.8 | 24.0 / 31.8 / 25.3 | 1.6-3.7 | 30/30 |
| `qwen3.5:9b` GPU, P2 en, warm | 3 | 4.7 / 3.9 / 22.4 | 38.8 / 35.9 / 56.2 | 3.4-3.8 | 30/30 |
| `qwen3.5:4b` CPU (`num_gpu 0`), P1 nl | 1 | 133.0 | 1310.9 (every block skipped) | 126.4 (two attempts) | 0/19 |

- VRAM of `qwen3.5:9b` with `num_ctx` 16384: 6.1 GB (011 F-08: 5.8 GB).
- JSON validity with `format`: 70/71 replies, and the one invalid reply passed on its retry.
- **Dutch first look** (N01, N02, N03, N06, N08, N09, N10, N16, N21, N23; model layer only, harness checks, no dictionary
  layer): recall 0/8, no `error` on the licence traps.
  - The model reported the typos (`wiet` -> `weet`) as `misses` verdicts, although no dictionary miss was listed.
    In the product the dictionary layer lists them; `wiet` is itself a Dutch word, so it would not be listed and stays a
    model task.
  - A prompt variant that asks for such words as items raised recall to 1/8, but produced `error` on 3 of 3 licence trap
    spans. It was reverted.
  - This matches R-06: Dutch grammar and d/t with the local 9b is weak. T068 measures it on the full suite; expect the
    standing quality note and owner question Q2 (T080).

**Outcome.**
- **BlockLines = 8 stays.**
- **SC-004:**
  - first model suggestion <= 6 s p50: met (nl 1.5 s, en 4.7 s);
  - full check <= 30 s p50: met for nl (25.3 s), missed for en (38.8 s);
  - p95 <= 45 s: missed in 1 of 6 warm runs (56.2 s, an outlier while the CPU run was being set up).
- The en sheet has more and longer explanations: about 3.5 s per block over 10 blocks.
- **CPU fallback `qwen3.5:4b`**: unusable as configured.
  - About 60 s per attempt on the CPU, so the 60 s block timeout ends or skips every block.
  - The replies that came back did not parse (0/19); the cause was not diagnosed in the timebox.
  - SC-004's "CPU fallback full check <= 180 s" is missed by a wide margin.
  - Follow-ups for T068 / the CTO: record the raw 4b replies (format support or truncation), raise the block timeout for
    the fallback (e.g. 180 s), or show "model layer paused while a GPU job runs" instead of running 4b on the CPU.
- Proposal (configuration, no contract change; CTO decision): let `SheetBlocks` merge consecutive short sections up to
  8 lines. P1/P2 have about 4 lines per block, so this gives about 6 requests per sheet and brings both under 30 s.
  Alternatively set the SC-004 en full check to <= 40 s p50.

**Fixtures (T009).** `tests/fixtures/lyrics/assist/` (12 sheets, 4 invented library songs, 300 + 300
syllable gold words, 51 nl + 40 en rhyme pairs, `expected.json`) and `e2e/lyric-scripts/` (10 scripts,
validated by `contracts/check_output_schema.py`). All texts are self-written (CC0). Syllable counts are
hand-checked, not yet against the real counters (backend T038 is the first run).
