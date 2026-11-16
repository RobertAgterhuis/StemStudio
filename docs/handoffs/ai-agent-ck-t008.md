# Hand-off: ai-agent / 011 follow-up claude-key, early note T008 (ClaudeApiKey available)

Branch `ck-lane-agent` (worktree `.claude/worktrees/ck-agent`, from `011-claude-key-spec`). Nothing pushed or merged.

## For the backend lane (T015, T023)

`ClaudeApiKey` is implemented in commit **6346587f** (red tests: 63bb309b). Cherry-pick or rebase onto 6346587f.

- `ClaudeApiKey.Parse(string?)` trims, then checks in this order and returns the first failed code of
  `ClaudeKeyInvalidReasons`: `empty` -> `prefix` (ordinal, case-sensitive `sk-ant-`) -> `length` (40-256 after trimming)
  -> `characters` (only `A-Z a-z 0-9 - _`; inner whitespace fails here) -> `admin-key` (prefix + `admin`, any case).
- `Reveal()` is the only member returning the value. `LastFour` = last four characters of the trimmed key.
  `Fingerprint` = lower-case hex SHA-256 of the UTF-8 key (memory only). `ToString()` = `[redacted]`. Reference equality.
- Constants `ClaudeApiKey.MinLength` (40) and `MaxLength` (256) added.
- `ClaudeKeySentinels.Stored/Environment/Replacement` parse as valid; `ClaudeKeySentinels.Admin` gives `admin-key`.

## Tests

`dotnet test tests/StemStudio.Agent.Tests -c Release --filter "FullyQualifiedName~ClaudeKey|FullyQualifiedName~ClaudeApiKey"`
-> 53 passed. Full run and coverage numbers follow in the foundational hand-off. `scripts/secret_scan.py` clean.

## Next

T009 -> T010 (`ClaudeKeyProvider`), then the foundational hand-off.
