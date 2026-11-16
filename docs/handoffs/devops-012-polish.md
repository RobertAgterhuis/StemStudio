# Hand-off: devops / 012 practice tempo, polish (T067)

Branch `012-lane-devops`. Nothing pushed or merged; the owner's `stemstudio` stack was not touched.
Foundational part: `devops-012-foundational.md`.

## What was built

- `scripts/check-soundtouch-served.sh` (+x) and `.ps1` (same checks and messages) against a running
  web (default `http://localhost:8080`):
  - `/js/soundtouch/soundtouch.wasm` is `200 application/wasm`; with `--expect-wasm FILE` /
    `-ExpectWasm` byte-identical to that file;
  - `/third-party/soundtouch/index.html` is `200 text/html`;
  - `SHA256SUMS` lists COPYING.TXT, NOTICE.md, README.md, soundtouch.lock, the archive named in
    the lock, the header, the shim and both build scripts; the archive's entry equals the lock's
    `SOUNDTOUCH_SHA256`; every listed file is served with exactly its SHA-256.
  - All failures are listed; exit 0 / 1 / 2 (usage, missing lock or expect file).
- CI `e2e` job: downloads the `soundtouch-wasm` artifact of the `soundtouch` job and runs the check
  against the compose stack, so the image's module is proven equal to the reproducible build.
- Image reproducibility: two image builds (`--no-cache --target soundtouch`) gave the same
  `soundtouch.wasm` and the same `SHA256SUMS`, equal to the script's Docker build.
- Non-Docker path documented in `--help` / `Get-Help` of `build-soundtouch.*` (`--local` / `-Local`).

## Files changed

- `scripts/check-soundtouch-served.sh` (new), `scripts/check-soundtouch-served.ps1` (new)
- `scripts/tests/test_check_soundtouch_served.py` (new)
- `.github/workflows/ci.yml` (e2e: download-artifact + check step)
- `specs/012-practice-tempo/tasks.md` (T067 ticked)

## Tests and coverage

```text
scripts/tests/test_check_soundtouch_served.py      8 passed (local http.server on a random port:
                                                   complete tree, tampered file, missing file,
                                                   unlisted required file, wrong pinned archive,
                                                   missing module + different expect file,
                                                   unreachable server, usage errors)
full scripts suite                                 73 passed; key_volume_checks.py 100 % line, 100 % branch
bash check vs throwaway container of the locally built image (stand-in shim)
  ok js/soundtouch/soundtouch.wasm (200 application/wasm); equals the local reproducible build;
  index.html 200 text/html; 9 listed files sha256 ok -> "all soundtouch checks passed", exit 0
pwsh check, same container                         "all soundtouch checks passed", exit 0
pwsh check vs http://127.0.0.1:9                   3 failures listed, exit 1
image builds: web:test and --no-cache soundtouch stage
  soundtouch.wasm 07c00b3a67ee0cf8d52e5e1a190b4e781be7438148a989e4a7fa4f3eb4bf819c both;
  SHA256SUMS a61e34c2651f9095b2efc5824a6cae1d76664e6a605af60492775c0cbcb44d96 both
```

The run on the integrated tree with the real shim happens in CI (`e2e`) and the CTO's T070.

## Quality checks

shellcheck v0.11.0 exit 0; PSScriptAnalyzer 1.25.0 (Warning, Error) 0; actionlint 1.7.12 0 errors;
ruff / ruff format / mypy --strict clean.

## Dependencies added

actions/download-artifact v8.0.2 (MIT), CI only (also listed in the foundational note).

## Docs impact

Technical manual (setup / operations): `scripts/check-soundtouch-served.sh [BASE_URL]` verifies a
running installation serves the module and the complete LGPL source package.

## Open issues / requests

See the foundational note (frontend: directory URL `/third-party/soundtouch/` is 404, link
`index.html`; audio-engine: `SOUNDTOUCH_WASM_REQUIRED=1`).

## Next owner

CTO (T069-T075).
