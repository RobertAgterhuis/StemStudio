# Lyric assistant fixtures (feature 023, T009)

Owner: architect (023 Phase 0, T009); maintained by qa afterwards. All lyric text in this folder is
self-written for StemStudio and dedicated to the public domain (CC0 1.0). No line of any existing
song is used; the "library" tracks under `corpus/` are invented songs that stand in for 006 track
lyrics. Files are UTF-8 with LF line ends and a final newline.

Line numbers everywhere are 1-based in the normalised sheet text; marker lines (`[verse]`) and blank
lines count, as in the backend anchors (data-model `Anchor`). `expected.json` holds the facts of
every sheet and corpus track (planted corrections, licence spans, block ranges, guard matches); the
tests should read the facts from that file instead of copying them.

## Sheets (`sheets/`)

| File | Language, title | What it encodes | Used by |
|---|---|---|---|
| `zomeravond.nl.txt` | nl, "Zomeravond" | planted: `fluisterd` (l.3), `vergeeten` (l.14) spelling; `word` -> `wordt` (l.10) grammar; l.11 stumbling line (flow); licences `'k`, `'t` (l.4), `Gij zijt` (l.8), `Naar huis ik ga` (l.9); unknown name `Zelmara` (l.15, for "add to my words"); l.2 has 15 syllables against the 12-syllable rhythm target of quickstart Q3; blocks 1-5, 7-11, 13-15 | spec US1 independent test, quickstart Q1-Q3, backend T025/T027/T029, ai-agent T031, frontend T033, qa T037/T044/T053 |
| `avondlied.nl.txt` | nl, "Avondlied" | second Dutch sheet with `Zelmara` (Q2 step 3: a word in "my words" is not flagged) | qa T037, backend T029 |
| `radio.en.txt` | en, "Radio" | section 1 counts 10/12/10/10 (median 10; l.3 two syllables long, spec US2 independent test); licences `Gone are the days` (inversion), `gonna`, `'til`; writing help on l.8; blocks 1-5, 7-11 | ai-agent T040/T049, frontend T042/T051, qa T044/T053, eval E31/E32 |
| `p1-havenlicht.nl.txt` | nl, "Havenlicht" | performance sheet P1: 40 lyric lines in 5 sections of 8 (5 blocks); 3 spelling (`straaten`, `ochtent`, `vergeeten`), 2 grammar (`word`, `Jullie hebt`), 4 licences (`'k`, `'t`, `Gij zijt`, `effe`); about 1,250 characters | SC-004, eval P1, T008 spike, T068 |
| `p2-harbour-light.en.txt` | en, "Harbour Light" | performance sheet P2, same structure: `untill`, `recieve`, `definately`; `wake` -> `wakes`, `are` -> `is`; `gonna`, `Thou art`, `'cause`, `Runnin'` | SC-004, eval P2, T008, T068 |
| `inject.en.txt` | en, "l-inject" | l.3 instruction injection (L-I1), l.5 fake JSON output block (L-I3), one genuine typo `sleaping` (l.4) so a valid suggestion exists | ai-agent T031 (adversarial fake), qa Q5 step 3, agent S4 / L-I4 |
| `inject.nl.txt` | nl | l.2 Dutch role-switch injection asking for a known chorus (L-I2) | ai-agent T031, backend T047 |
| `verbatim.en.txt` | en, "Kettle" | own sheet, no library match; lines 1-2 equal lines 1-2 of `not-own.en.txt` so `e2e/lyric-scripts/alternatives.json` fits both | L-V2, quickstart Q5 step 2, backend T047, ai-agent T049 |
| `not-own.en.txt` | en, "Lanterns" | 4 of 8 lines (50 %) are full lines of `t-fake-song` -> `409 not-own-lyrics`, overridable | L-V1, L-V3, quickstart Q5 steps 1 and 4-7, backend T047/T074 |
| `not-own-two-tracks.en.txt` | en | 4 lines of `t-fake-song` and 3 lines (30 %) of `t-fake-song-2`: with an override for `t-fake-song` still refused, naming `t-fake-song-2` | L-V4, backend T074 |
| `cliche-sheet.en.txt`, `cliche-sheet.nl.txt` | en, nl | self-written cliche lines; exactly 2 full lines of 5+ words match the cliche track, the rest are short (< 5 words) or own -> must pass the sheet guard (needs >= 3 lines and >= 25 %) | research R-07 cliche set, backend T047 |

## Library corpus (`corpus/`, stand-in for 006 track lyrics)

| File | Track id, title | Notes |
|---|---|---|
| `t-fake-song.en.txt` | `t-fake-song`, "Paper Lanterns" | 8 lines of 7-9 words; line 1 is the 9-word verbatim run `we light the paper lanterns down by the river` |
| `t-fake-song-2.en.txt` | `t-fake-song-2`, "Salt and Static" | second track for L-V4 |
| `t-fake-cliches.en.txt`, `t-fake-cliches.nl.txt` | `t-fake-cliches`, `t-fake-cliches-nl` | cliche lines for the false-positive test |

The fake stack seeds a track's lyrics by pasting this text through the 006 `setLyrics` endpoint
(plain text), because the worker's fake lyrics provider (`STEMSTUDIO_FAKE_LYRICS`, `fake-lyrics.json`)
returns only 3-word lines, which can never trigger the guard (lines of 5+ words, 7-word shingles).

## Gold lists and vectors

| File | Content | Used by |
|---|---|---|
| `syllables-nl.tsv` | 300 Dutch words, `word<TAB>syllables<TAB>note`: monosyllables with every digraph and trigraph (`aa ee oo uu ie oe eu ui ij ei au ou`, `aai ooi oei ieu eeuw`, `-uw`, `-auw`/`-ouw`), diaeresis (`ruïne`, `België`, `geïnteresseerd`, `ideeën`, `coördinatie`), `-ij`/`-lijk` and `y`, French and English loanwords (`cadeau`, `café`, `baby`, `team`, `cake`, `love`, `computer`), elisions and spoken forms (`'k` = 0, `'t` = 0, `m'n`, `z'n`, `d'r`, `zo'n`, `da's`, `effe`, `lope`), archaisms (`gij`, `zijt`, `weleer`), unstressed prefixes (`be-`, `ge-`, `ver-`, `ont-`), compounds and long words up to 10 syllables | backend T038 (`SyllableCounterDutch`, SC-007 >= 97 %) |
| `syllables-en.tsv` | 300 English words: silent final `e`, `-le`, `-ed` (`walked` 1, `wanted` 2), `-es`, `-tion`/`-sion`, `-ia`/`-io`/hiatus (`idea`, `quiet`, `poet`), silent letters, contractions and licence forms (`gonna`, `wanna`, `'cause`, `ain't`, `o'er`, `e'en`, `runnin'`), and words that are not in CMUdict (`yeet`, `vibey`, `heartbreaky`, `neonlit`, `moonwalkin'`). 15 rows with note `ambiguous` (`every`, `fire`, `our`, `hour`, `poem`, `flower`, `favourite`, `evening`, `different`, `family`, `interesting`, `chocolate`, `camera`, `tired`, `cruel`) carry the first CMUdict pronunciation's count; tests accept either count for them | backend T038 (`SyllableCounterEnglish`, SC-007 >= 95 %) |
| `rhymes-nl.json` | 51 pairs `{a, b, expected: perfect/near/none, note?}` covering the R-04 rhyme-key rules: `ei = ij`, `au = ou`, final `d = t`, final `g = ch`, silent `-n` of `-en`, stress on the penultimate for `-e/-en/-er/-el`, unstressed prefixes | backend T038 (`RhymeKeyDutch`), T045 (`RhymeFinder`) |
| `rhymes-en.json` | 40 pairs: perfect (equal part from the last stressed vowel, incl. stressed final syllables `upon`/`on`), near (same vowel, other final consonant; or same consonants, other vowel), none | backend T038 (`RhymeKeyEnglish`), T045 |
| `expected.json` | facts of every sheet and corpus track (see above) | all of the above |

