# ADR-037: Lyric writing assistant (feature 023)

**Status**: Accepted (owner, 2026-11-16, with the answers to Q1-Q3 below; Phase 0 T011 adds a
"Phase 0 outcome" note with the measured numbers). ADR
number provisional: 035 is the 011 claude-key follow-up, 036 is left for feature 022 designed in
parallel; the CTO renumbers at merge if needed (ROADMAP rule).

**Context**: owner request 2026-11-16 - on-demand help with writing lyrics, plus checking spelling,
grammar and flow in the sheet's language (Dutch, English), with poetic licence always allowed and
proposed by the assistant itself; own lyrics only (010 sheets), never imported 006 lyrics.
Constraints: constitution 1.4.0 (local-first, Ollama default, Claude opt-in, OSI licences, honesty),
011 agent runtime, policy gate and guardrails, 011 claude-key follow-up.

## Decisions

**D1 - A tool-less assistant inside the agent runtime.** `LyricAssistant`
(`StemStudio.Agent/LyricAssist/`) reuses the 011 backend selection, model selector (default
`qwen3.5:9b`, CPU fallback `qwen3.5:4b` while a heavy GPU job runs) and Claude key resolution, but
runs its own prompts with **no tools**. It is reached from the 010 lyrics editor through dedicated
endpoints and from the agent through thin tools. Being part of the AI composer agent's runtime, it
is covered by the constitution's Claude exception without an amendment. Rejected: routing editor
actions through agent runs (sheet text would enter the agent model's context, free text instead of
anchored suggestions); a separate service (duplicate backend and key handling).

**D2 - Deterministic layer first, in process.** Spelling with WeCantSpell.Hunspell (MPL-1.1 chosen
from its tri-licence) and vendored dictionaries (OpenTaal nl, BSD-3-Clause; SCOWL en_US + en_GB,
MIT-like); syllables (Dutch rules, CMUdict for English, BSD-2-Clause-style); rhymes (CMUdict;
Dutch spelling rhyme keys on the OpenTaal word list); licence lexicon, intentional marks, personal
words. It runs without a model, so the assistant degrades gracefully. **LanguageTool is not used**
in this feature (1 GB JVM service, many false positives on lyric language); it is a follow-up gated
by the measured Dutch quality: owner decision 2026-11-16 "no for now"; decision point T080 after
the lyric eval run, triggered only when Dutch grammar recall of `qwen3.5:9b` is below 60 %.

**D3 - Suggestions, never edits.** All output is stored as anchored suggestions (line, UTF-16
offset, length, original text). The text changes only through accept / accept-all (server copies
the stored proposal) and every accept is one revision and one undo step. Suggestions re-anchor on
saves; a suggestion whose original is gone becomes stale and is never applied.

**D4 - Schema-constrained output, verified claims.** The model answers per block of <= 8 lines in
the JSON format `lyric-assist-output` 1.0.0 (Ollama `format`; Claude: one forced tool). The server
validates schema and anchors, applies the sensitivity and licence filters, verifies rhyme and
syllable claims deterministically, and drops anything that fails.

**D5 - Poetic licence by construction.** Licence handling is layered (never-checked spans, licence
lexicon, code-switch, sound spelling, model classification of dictionary misses, prompt rules,
sensitivity filter); every layer can only soften, never add an error. Recognised licences are at
most `idea` notes (strict). Intentional marks (word, phrase, line) are stored per sheet and respected
while the text exists; a personal word list applies per language. The assistant proposes licences
itself (category `licence`) and devices (category `device`) as optional suggestions.

**D6 - Provenance and the 011 guardrails.** The assistant reads a sheet only on an explicit user
request (provenance "user-authored sheet"). `lyrics-not-from-user` is unchanged: the agent can put
text into a sheet only by `apply_lyric_suggestions`, which carries ids only, always needs approval
(new reason `lyrics-edit`) and shows before/after from the stored suggestions. Agent tool results
carry no lyric text. Lines produced by accepted suggestions are recorded (`AssistedLines`) and a
song take snapshots the assisted line count.

**D7 - Copyright and imitation.** 006 lyrics are never accepted as input. A sheet matching library
track lyrics (>= 3 lines of 5+ words and >= 25 % of lines) is refused (`not-own-lyrics`). Generated
text with a 7-word run or a full 5+-word line of library lyrics is withheld. Instructions pass the
009/011 imitation guard. Lyrics outside the library cannot be detected; documented.
**Own-work override (owner decision Q1, 2026-11-16):** for a false positive (e.g. the owner's own
band's song imported as a track) the user may confirm in a dialog that the text is their own work
(statement `own-work.v1`, typed name, tick box). It is stored per sheet (name, time, matched track
id and title), shown on the sheet, revocable, and covers only the matched track; it never disables
the 7-word verbatim hold-back on generated text nor the imitation refusal; no agent tool can create,
revoke or read it (`PUT/DELETE /api/lyrics/{id}/own-work-override`).

**D8 - Privacy and Claude.** Nothing runs automatically. Without Claude selected nothing leaves the
machine. With Claude, the first model action requires acknowledging the lyric-assistant notice
(version `lyric-claude-notice.v1`, stored locally); per-assist token caps (20k in / 4k out) and
visible usage. No automatic switch between backends.

**D9 - Quality is measured, not assumed.** A 64-case lyric evaluation set (32 nl, 32 en, plus
guardrail and performance cases) runs in `tools/agent-eval --suite lyric` on the reference machine.
Local targets: recall spelling >= 75 %, grammar >= 60 %, precision >= 80 %, licence false positives
<= 10 %. A miss for Dutch shows a standing quality note in the pane and triggers the LanguageTool
owner question. Every suggestion shows its layer and model.

**D11 - Explanation language (owner decision Q3).** Explanations are written in the sheet's
language; the UI, category names and help stay English (MoSCoW W7).

**D10 - Versions.** backend-api 1.23.0, ui-test-ids 1.23.0, tool catalogue 1.2.0 (1.1.0 reserved
for 022), agent eval set 1.1.0, lyric-assist-output 1.0.0; all additive.

## Consequences

- Positive: local, deterministic baseline; injection cannot act; text never changes silently;
  honest per-suggestion provenance; no new container.
- Negative: Dutch grammar quality depends on a 9B local model until measured; Dutch rhymes are
  spelling-based approximations; ~10 MB of vendored dictionary data in the repository; one more
  prompt family to maintain.
- Follow-ups: optional LanguageTool service (only via decision point T080); remembering rejections across checks
  (YAGNI now); more languages (only with open dictionaries and an eval set).
