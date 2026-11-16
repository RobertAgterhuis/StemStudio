# Hand-off: ai-agent / 011 follow-up claude-key, checkpoint foundational (T007-T010)

Branch `ck-lane-agent` (worktree `.claude/worktrees/ck-agent`, from `011-claude-key-spec`). Nothing pushed or merged.
Anthropic was never called; no key-shaped literal was written (all keys from `ClaudeKeySentinels`).

## Commits

| Commit | Task |
|---|---|
| 63bb309b | T007 red: `ClaudeApiKeyTests` |
| **6346587f** | **T008 `ClaudeApiKey` implemented** (early note: `ai-agent-ck-t008.md`, d5c5304c) |
| 1b537173 | T009 red: `ClaudeKeyProviderTests` (+ throwing stub) |
| 6bc91f54 | T010 `ClaudeKeyProvider` |
| 611199f2 | refactor: rule table in `ClaudeApiKey`, smaller provider units (SIG unit size / complexity) |

## What was built

- `src/StemStudio.Agent/Model/ClaudeApiKey.cs`: `Parse` (trim, then empty -> prefix -> length 40-256 -> characters ->
  admin-key), `Reveal`, `LastFour`, `Fingerprint` (lower-case hex SHA-256 of UTF-8), `ToString()` = `[redacted]`,
  constants `MinLength`/`MaxLength`. The prefix is assembled from parts; the character class is checked with
  `char.IsAsciiLetterOrDigit` + `-`/`_` instead of a regex (same rule, no regex needed).
- `src/StemStudio.Agent/Backends/ClaudeKeyProvider.cs` (`IClaudeKeyProvider`): the data-model 3 state table; environment
  wins; whitespace env = unset; `storeAvailable`/`storeUnavailableReason` passed through; readable `reason`/`fix`
  (contract example texts for `stored-unreadable`; "The key store is not available: the key ring directory ... The
  ANTHROPIC_API_KEY environment variable still works." for an unusable store). Cached until `Invalidate()` or a change of the
  environment value (the stored key is read only when the cache is rebuilt). `KeyFor(active|environment|stored)` returns
  null for a source without a usable key. The internal snapshot is a class, so no generated `ToString` prints a value.
- Not yet wired into DI or the factory/probe/status service: that is T021/T022 (us-p1).

## Decisions to review (architect)

1. **The environment value is format-checked like a stored key.** A malformed `ANTHROPIC_API_KEY` (wrong prefix, length,
   characters, admin key) is not used: the state falls through to the stored key (or `not-configured`) and `reason` says
   "ANTHROPIC_API_KEY is set but is not a usable Claude key (rule: <code>), so it is not used." with a fix. Why: the
   contract's `lastFour` pattern `^[A-Za-z0-9_-]{4}$` must hold for every source, and an admin key must never be used.
   011 accepted any non-blank value; real Anthropic keys are unaffected.
2. **One `reason`/`fix` pair, most actionable first**: unusable store > unreadable stored key > malformed environment
   value. With an environment key and an unreadable stored key the unreadable reason/fix is still reported (US5 scenario 4).

## Tests and coverage

| Command | Result | Line | Branch |
|---|---|---|---|
| `dotnet test tests/StemStudio.Agent.Tests -c Release -p:CollectCoverage=true` | 681 passed, 0 failed | StemStudio.Agent **98.83 %** | **93.57 %** |

`ClaudeApiKey` and `ClaudeKeyProvider` are both 100 % line and branch. `ClaudeKeySkeletonTests` lost its stub-throws test
and now uses a parsed sentinel; `Fakes/ClaudeKeyTestDoubleTests` likewise.

## Quality checks

- Build (TreatWarningsAsErrors) clean; `dotnet format --verify-no-changes` on `src/StemStudio.Agent` and
  `tests/StemStudio.Agent.Tests` exit 0.
- `tools/quality/run_all.py`: 0 errors; no finding in the new source files (one duplication warning inside
  `ClaudeKeyProviderTests`, test code).
- `scripts/secret_scan.py`: clean.

## Dependencies added

None.

## Requests

- **backend**: `ClaudeApiKey.Parse` is ready for T015/T023 (commit 6346587f). For the DI wiring in T022 the API host must
  register `IStoredClaudeKeyReader` (`ProtectedClaudeKeyStore`); until it does, the agent library will register a default
  reader that reports "nothing stored, store unavailable" (T022). The provider is the place to call `Invalidate()` after
  `PUT`/`DELETE`.
- **architect**: review decisions 1 and 2 above (T053 fold).

## Next

T021 -> T022 (factory, probe and status service through `IClaudeKeyProvider`; `ClaudeStatusDto.Key`), hand-off `us-p1`.
