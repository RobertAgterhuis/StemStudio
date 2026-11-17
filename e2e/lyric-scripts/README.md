# Fake lyric-assistant scripts (feature 023)

Scripted output of the fake assistant model for the E2E fake stack (`STEMSTUDIO_AGENT_BACKEND=fake`,
`STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT=/scripts/lyric`, mounted from this folder; contract DELTA.md
section 4). The variable names a file (every assist uses it) or this folder: per assist the fake uses
`<title-slug>-<kind>.json`, else `<kind>.json`, else no script (every block answers `{"items":[]}`);
`LyricScript.Resolve` / `LyricScript.Slug` in `StemStudio.Agent/LyricAssist/Fakes` (slug: lower-case ASCII
letters and digits, other runs -> `-`). Blocks beyond a script answer `{"items":[]}`; a retry of a block
gets the same answer. Format: `{"version": 1, "blocks": [<output>]}`, where every `<output>` is one
model answer valid against `specs/023-lyric-writing-assistant/contracts/lyric-assist-output.schema.json`;
block *k* answers the *k*-th block request of an assist (sheet blocks of at most 8 lines along
section and blank-line boundaries). Anchors (`line`, `original`) match the fixture sheets in
`tests/fixtures/lyrics/assist/sheets/` exactly; spelling corrections are classified through
`misses` (the dictionary layer finds the miss, the model classifies it).

| Script | Scenario | Sheet | Blocks | What it must produce |
|---|---|---|---|---|
| `zomeravond-check.json` | Q1, Q2 (US1) | `zomeravond.nl.txt` | 3 | misses `fluisterd` -> `fluistert`, `vergeeten` -> `vergeten` (typo); `word` -> `wordt` (error); flow improvement on l.11; licence ideas `Gij zijt`, `Naar huis ik ga` (hidden at balanced, shown at strict) |
| `zomeravond-devices.json` | Q3 step 2 (US2) | `zomeravond.nl.txt` | 3 | syllable fit on l.2 (15 -> 12 by the server's counter); end rhyme on l.5 with `licht` (l.4), verified |
| `zomeravond-alternatives.json` | Q3 step 3 (US3) | `zomeravond.nl.txt` | 1 | three alternatives for l.4 (one group) |
| `zomeravond-continue.json` | Q3 step 4 (US3) | `zomeravond.nl.txt` | 1 | a 2-line continuation inserted after l.5 (`original` empty) |
| `avondlied-check.json` | Q2 step 3 | `avondlied.nl.txt` | 1 | no model items (only the dictionary layer matters) |
| `radio-devices.json` | spec US2 independent test | `radio.en.txt` | 2 | `licence.elision` on l.3 (`o'er`, 12 -> 10); end rhyme on l.9 with `on` (l.8) via `upon` |
| `radio-alternatives.json` | US3 (eval E31) | `radio.en.txt` | 1 | three alternatives for l.8 |
| `radio-titles.json` | US3 (eval E32) | `radio.en.txt` | 1 | five titles (`line` 0, `original` = the current title `Radio`) |
| `alternatives.json` | Q5 steps 2 and 5 (L-V2, L-V3) | `verbatim.en.txt` or `not-own.en.txt` (same lines 1-2) | 1 | alternative 1 is the 9-word run of `t-fake-song` -> withheld (`discarded.verbatim = 1`); alternative 2 is shown |
| `l-inject-check.json` | Q5 step 3 (L-I1, L-I3) | `inject.en.txt` | 1 | only `sleaping` -> `sleeping` is shown; the three `DELETE` items are discarded (whole-line correction over 60 % of the line, anchor `zzz` not in the text, line 99 out of range) |

The scripts are self-written (CC0). `tests/fixtures/lyrics/assist/expected.json` lists the block
ranges and spans the scripts rely on.
