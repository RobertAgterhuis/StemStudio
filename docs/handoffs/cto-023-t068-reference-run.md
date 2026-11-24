# 023 T068: reference-machine run of the lyric suite (2026-11-24)

Machine: owner PC, RTX 5070 Ti 16 GB, Ollama local. Command:
`dotnet run --project tools/agent-eval -- --backend ollama --model <model> --suite lyric --runs 3`.
Grading self-test (`--backend fake`) first: 64 cases, 0 grading failures.

Raw reports: `cto-023-t068/qwen3.5-9b.md` (the SC-001 model) and `cto-023-t068/qwen3.5-9b-64k.md`
(a local 64k-context variant, run first; same outcome).

## Result against SC-001 / SC-004 (qwen3.5:9b, median of 3 runs)

| Scope | Recall | Precision | Licence FP | SC-001 target |
|---|---|---|---|---|
| nl spelling / grammar / flow | 0 % / 0 % / 0 % | 100 % | 50 % / 33 % / 67 % | recall >= 75 % spelling, >= 60 % grammar; licence FP <= 10 % |
| en spelling / grammar / flow | 0 % / 0 % / 0 % | 100 % | 67 % / 0 % / 33 % | same |
| nl / en devices, writing, licence | 100 % | 100 % | 0 % | - |
| Latency per case | p50 1.5 s, p90 2.2 s | | | SC-004 met for short cases |

**SC-001 fails in both languages.** Precision is 100 % only because the model reports no `error`
items at all. SC-004 (latency) is met.

## Cause (raw replies inspected with a temporary harness logger, not committed)

1. **Prompt bias towards licence.** Both `qwen3.5:9b` and `qwen3.5:4b` label every line
   `licence.elision` with severity `idea`, also clear typos ("'recieve' is a deliberate
   misspelling") and lines without a problem ("Geen fout, maar ..."). The licence rules in
   `LyricPromptBuilder.LicenceRules` dominate the task line; no item reaches severity `error`.
2. **Anchor copy with the line prefix.** `qwen3.5:9b` copies the numbered-sheet prefix into
   `original` (`"*1| Ik wiet niet ..."`), so `SuggestionValidator` discards the item as an invalid
   anchor. `qwen3.5:4b` does not.
3. **Harness without the dictionary layer** (by design, R-09): the 9b model did classify
   `wiet -> weet` as a `typo` in `misses`, but the harness hands no misses and does not count them.
   In the app Hunspell finds that typo, so the spelling recall a user sees is higher than the
   harness number. The harness also swallows `LyricAssistException` messages silently.

## Actions taken

- Standing quality note switched on for the Ollama backend
  (`LyricAssistStatusService.LocalModelQualityNote`, test
  `OllamaBackend_ShowsTheStandingQualityNote_OtherBackendsDoNot`); user manual chapter 20 updated.
- T080 (LanguageTool): Dutch grammar recall is below 60 %, so the decision point is triggered and
  presented to the owner. CTO advice: defer it. The measured cause is the prompt, not missing grammar
  knowledge (English fails the same way), so LanguageTool would not fix it; decide again after 024.
  **Owner decision 2026-11-24: deferred** until after the 024 reference run.
- Follow-up feature **024 lyric assistant quality** on `docs/ROADMAP.md` (owner decision
  2026-11-24): prompt and anchor fixes, the dictionary layer and failure logging in the harness, then
  this run again.
