# Hand-off: devops / 011 follow-up claude-key, foundational (T003, T019, T020)

Branch `ck-lane-devops` (worktree `.claude/worktrees/ck-devops`, from `011-claude-key-spec`). Nothing pushed or merged.
The owner's compose project `stemstudio` (:8080/:8081) was **not** stopped, recreated, rebuilt or
inspected beyond `docker ps`; none of its volumes was touched, and no image tag `stemstudio/*` was rebuilt.
Anthropic was never called; no key-shaped literal was written.

## What was built

- **T003 spike** (with the architect's questions), recorded in `research.md` R-05 "Spike T003 findings" F-01..F-07.
  Throwaway compose project `ck-spike` on host ports 18080/18081 with a probe ASP.NET Core app (.NET 10, aspnet image,
  non-root `$APP_UID`) as `api` and `web`, the same `/keys` Dockerfile lines as T020 and a named volume at `/keys`.
  Removed afterwards (`docker compose -p ck-spike down -v`, probe image deleted). Results:
  - **F-01** Every request from the host through a published port (`localhost`, `127.0.0.1`, `[::1]` and the host's own LAN
    address) arrives from the compose network **gateway** (`::ffff:10.200.5.1`), never loopback, no `X-Forwarded-For`.
    The API cannot tell host from LAN by address; the gateway must not be allow-listed. A second LAN device was not
    available; that row stays quickstart Q9b.
  - **F-02** A `web` -> `http://api:8081` call arrives from the web container (`::ffff:10.200.5.3`); `Dns.GetHostAddresses("web")`
    returns `10.200.5.3`; `Host` is `api:8081`.
  - **F-03** The `Host` header is passed through unchanged (a forged `Host: localhost` to the LAN address arrives as
    `localhost`): the loopback host-name check of the Web page is the only browser-side signal, and it is forgeable (T7).
  - **F-04** An empty named volume at `/keys` is initialised `700 1654:1654` from the image; key files are `0600`. Also when
    the empty volume was first mounted into an image without `/keys`.
  - **F-05** A protected payload survives `docker compose down` + `up --build --force-recreate`, even with the content root
    moved from `/app` to `/app2`; a different application name -> `CryptographicException`.
  - **F-06** `VOLUME ["/keys"]` gives any container of the api image without an explicit mount an anonymous ring
    (a one-off `docker compose run api` sees the stored key as `stored-unreadable`).
  - **F-07** Git Bash rewrites container paths in environment variables (CoreCLR failed to start): `MSYS_NO_PATHCONV=1`.
- **T019** `scripts/key_volume_checks.py` (standard library only) + `scripts/tests/test_key_volume_checks.py` + tool config
  `scripts/pyproject.toml` (pytest, coverage `fail_under = 90`, ruff with the SIG limits, mypy strict; runs in the
  tools/quality environment). Checks over `docker compose config --format json` (piped, never printed) and the files:
  volume `api-keys` declared; `api` mounts it as a named volume at `/keys`; no other service mounts it or anything at
  `/keys`, `api` mounts it once; `StemStudio__DataProtection__KeyRingDir=/keys`; `StemStudio__Agent__KeyEntry__InternalCaller=web`
  and service `web` exists; runtime stage `install -d -m 0700 -o "$APP_UID" -g "$APP_UID" /keys`, `VOLUME` with `/keys`,
  last `USER $APP_UID`; `ANTHROPIC_API_KEY` a bare pass-through in every compose file and never assigned (the output never
  contains its value; tested). Wrappers `scripts/check-key-volume.{sh,ps1}` (`-f`/`-File` for compose combinations).
- **T020** `docker/api.Dockerfile`: `/keys` 0700 for `$APP_UID`, `VOLUME ["/data", "/keys"]`,
  `ENV StemStudio__DataProtection__KeyRingDir=/keys`. `docker-compose.yml`: volume `api-keys` at `/keys` in `api` only, the
  two `StemStudio__*` variables, `ANTHROPIC_API_KEY` unchanged as bare pass-through, comment block (two key sources,
  environment wins, never copy `api-keys` with `api-data` or a backup, losing it = "Enter the key again", local-only
  entry and the T7 limit until 018, the check script).
- **CI / test-all** (architect request 2): `.github/workflows/ci.yml` contracts job lints the full backend 1.21.0 and the
  claude-key delta (`CLAUDE_KEY_CONTRACTS`); the quality job runs ruff, ruff format, mypy and pytest (90 % gate) of the key
  checks; the compose job runs `check-key-volume.sh` for the four combinations it validates. `scripts/test-all.{sh,ps1}`
  do the same. The secret scan job and steps are unchanged.

## Files changed

- New: `scripts/key_volume_checks.py`, `scripts/check-key-volume.sh` (mode 755), `scripts/check-key-volume.ps1`,
  `scripts/pyproject.toml`, `scripts/tests/test_key_volume_checks.py`, this note.
- Modified: `docker/api.Dockerfile`, `docker-compose.yml`, `.github/workflows/ci.yml`, `scripts/test-all.sh`,
  `scripts/test-all.ps1`; `specs/011-ai-composer-agent/followups/claude-key/research.md` (R-05 spike findings) and
  `tasks.md` (T003, T019, T020 ticked) by the caller's instruction (architect-owned folder).

## Tests and coverage

Only the devops checks were run (three other agents build on this PC; no full suite).

```
uv run --project tools/quality --frozen pytest -c scripts/pyproject.toml --cov --cov-branch --cov-config=scripts/pyproject.toml --cov-report=term scripts/tests
scripts\key_volume_checks.py     138      0     36      0   100%
45 passed
```

Line **100 %** (138/138), branch **100 %** (36/36). (The count includes the T050 tests; see `devops-ck-polish.md`.)
Red first: `d639c288` failed collection (`ModuleNotFoundError: key_volume_checks`), green in `8ed8d99e`.

## Quality checks

- `ruff check` / `ruff format --check` / `mypy --strict` (scripts/pyproject.toml) on `scripts/key_volume_checks.py scripts/tests` -> clean.
- `python tools/quality/sig_check.py scripts/key_volume_checks.py scripts/tests/test_key_volume_checks.py` -> 63 units, 0 errors, 0 warnings.
- `uv run --project tools/quality --frozen python tools/quality/run_all.py` -> 0 errors, 742 warnings (unchanged baseline), exit 0.
- `python scripts/secret_scan.py --self-test` passed; `python scripts/secret_scan.py` -> 3493 files clean.
- `docker compose config -q`: default; + gpu; + gpu + generative; + generative + generative-fake; + `docker/compose.model-cache-bind.yml`
  (with `STEMSTUDIO_MODEL_CACHE_DIR` set); and the fake agent variables -> all valid. `check-key-volume.sh` -> 10/10 ok for
  each combination; `check-key-volume.ps1 -File docker-compose.yml,docker-compose.gpu.yml` -> 10/10 ok.
- api image built under the separate tag `ck-devops/api:check` (not `stemstudio/api:local`): `/keys` = `700 1654:1654` on a fresh
  volume, `KeyRingDir=/keys`, image volumes `/data`, `/keys`, user 1654; image and volume removed.
- `npx @redocly/cli@2.78.0 lint --config .../claude-key/contracts/redocly.yaml backend-api.openapi.yaml backend-api.claude-key.openapi.yaml`
  -> valid, 11 warnings (the accepted `operation-4xx-response` 9 + 2).
- actionlint 1.7.12 (docker `rhysd/actionlint:1.7.12`, `-verbose`) `ci.yml e2e-real.yml` -> "Found 0 errors in 2 files".
- shellcheck v0.11.0 (docker `koalaman/shellcheck:v0.11.0`) `check-key-volume.sh dev.sh test-all.sh` -> exit 0.
- PSScriptAnalyzer 1.25.0 (Warning, Error) `check-key-volume.ps1`, `test-all.ps1` -> 0 findings.

## Dependencies added

None (standard library Python; pytest/pytest-cov/ruff/mypy are the existing tools/quality dev group).

## Docs impact

Setup change for the technical manual (CTO, T055): the new volume `stemstudio_api-keys` at `/keys` (api only, 0700, app user),
`StemStudio__DataProtection__KeyRingDir`, never copy it with `api-data` or a backup, `docker compose down -v` or
`docker volume rm stemstudio_api-keys` loses it (key must be entered again), `scripts/check-key-volume.{sh,ps1}`.
The compose comment block carries the same text (CTO review in T054).

## Open issues / requests

1. **backend (T028/T029, `LocalOnlyKeyEntryFilter`)** from F-01/F-02: compare the peer after `MapToIPv4()` (it is
   `::ffff:a.b.c.d`; DNS of `web` returns plain IPv4); compare `Host` without the port (`api:8081`, `localhost:8081`);
   never accept the compose gateway address (it carries host **and** LAN traffic). In Docker the loopback branch never
   matches a browser request; only the `web` addresses do.
2. **backend (T013/T014)**: configuration key as in data-model 1, `StemStudio:DataProtection:KeyRingDir`
   (`StemStudio__DataProtection__KeyRingDir`); the caller's brief said `KeysPath`, I followed the data model. The image
   also sets it to `/keys`, so `docker run` without compose uses `/keys` too.
3. **qa (T046)**: `docker compose exec api sh -c 'rm -f /keys/*.xml'` works as the app user (files are `0600 app`); from
   Git Bash prefix `MSYS_NO_PATHCONV=1`. `down -v` of `stemstudio-e2e` removes its `api-keys` too.
4. **quality-gate**: new tested Python under `scripts/` with its own config `scripts/pyproject.toml`; add
   `scripts/key_volume_checks.py` to the component map / Sonar sources if they list script files.
5. **architect**: R-05 F-01..F-07 written and T003 ticked by devops on the caller's instruction; please review the wording.
6. **owner's stack**: the next `docker compose up --build` of `stemstudio` creates `stemstudio_api-keys` (empty, initialised
   0700 app); nothing to migrate.

## Next owner

backend (T028 with F-01/F-02), qa (T046), CTO (T054 compose comment review, T055 technical manual).
