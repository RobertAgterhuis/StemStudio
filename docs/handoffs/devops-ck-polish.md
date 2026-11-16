# Hand-off: devops / 011 follow-up claude-key, polish (T050)

Branch `ck-lane-devops`. T052 was not selected (owner Q1 (b), keep LAN access until 018), so this is the last devops task.

## What was built

- `scripts/dev.sh` (`init_key_ring_environment`) and `scripts/dev.ps1` (`Initialize-KeyRingEnvironment`): when
  `StemStudio__DataProtection__KeyRingDir` is unset they set the API's per-user default (data-model 1) and always print
  `Claude key ring (api, per user; never copy it with data/): <dir>`. Defaults: Windows `%LOCALAPPDATA%\StemStudio\keys`
  (Git Bash too, via `$LOCALAPPDATA`), Linux/macOS `$XDG_DATA_HOME/stemstudio/keys` when absolute, else
  `~/.local/share/stemstudio/keys`. An existing value wins. The scripts do not create the directory (the API does and
  tightens it to 0700, backend T014). Header text updated (key entered in Settings or from the shell).
- `scripts/key_volume_checks.py` check 10 (`--repo-root`, passed by `check-key-volume.{sh,ps1}`): the override, else the
  default (`default_key_ring_dir`), must not be the repository, below it, or below the library `<repo>/data`.
- Small fix: `scripts/dev.sh --help` printed one line too few (the `--timeout` line).

## Files changed

`scripts/dev.sh`, `scripts/dev.ps1`, `scripts/key_volume_checks.py`, `scripts/check-key-volume.{sh,ps1}`,
`scripts/tests/test_key_volume_checks.py`, `specs/011-ai-composer-agent/followups/claude-key/tasks.md` (T050 ticked), this note.

## Tests and coverage

```
uv run --project tools/quality --frozen pytest -c scripts/pyproject.toml --cov --cov-branch --cov-config=scripts/pyproject.toml --cov-report=term scripts/tests
scripts\key_volume_checks.py     138      0     36      0   100%
45 passed
```

Line **100 %**, branch **100 %**. Red first: `28c3ea4f` (12 failed, 33 passed), green in `5c084f14`.
Manual: `check-key-volume.sh` -> `ok   non-Docker key ring outside repository and library`; with
`StemStudio__DataProtection__KeyRingDir=<repo>/data/keys` -> `FAIL ... is inside the repository`. The dev functions were run
in isolation: Windows -> `C:\Users\<user>\AppData\Local\StemStudio\keys`; `XDG_DATA_HOME=/x/y` -> `/x/y/stemstudio/keys`;
relative XDG -> `~/.local/share/stemstudio/keys`; an override is kept.

## Quality checks

ruff, ruff format, mypy strict clean; `sig_check.py` 0 errors 0 warnings; shellcheck v0.11.0 (`dev.sh`, `check-key-volume.sh`,
`test-all.sh`) exit 0; PSScriptAnalyzer 1.25.0 `dev.ps1`, `check-key-volume.ps1` 0 findings; `run_all.py` 0 errors (742
warnings, baseline); secret scan clean. `dev.* --smoke-test` was not run (it builds and starts the whole stack on :8080/:8081,
which the owner's Docker stack occupies).

## Dependencies added

None.

## Docs impact (technical notes for the CTO's manual edit, T055)

- Non-Docker: the API's key ring is a per-user directory outside the repository and the library:
  Windows `%LOCALAPPDATA%\StemStudio\keys` (the ring is protected with DPAPI for the current Windows user), Linux/macOS
  `$XDG_DATA_HOME/stemstudio/keys` or `~/.local/share/stemstudio/keys` (not encrypted at rest; directory 0700).
  `scripts/dev.*` print it at start; override with `StemStudio__DataProtection__KeyRingDir` (the API refuses a directory
  inside the library, the backups, the app folder or a git work tree and then disables key storage).
- Docker: volume `stemstudio_api-keys` at `/keys`, api only; never copy it with the library or a backup; recovery after a
  lost volume or a restore on another computer = enter the key again in Settings. `scripts/check-key-volume.{sh,ps1}`.
- Moving to another computer: copying the database without the key ring makes the stored key unreadable by design.

## Open issues / requests

- **backend (T013/T014)**: keep the API's own defaults identical to the list above (dev scripts set the variable explicitly,
  so a difference would only show up when the API is started without the scripts).

## Next owner

CTO (T054, T055, T056).
