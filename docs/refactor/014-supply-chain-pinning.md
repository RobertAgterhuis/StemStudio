# 014 Close the supply-chain gaps: image digests, NuGet lock files, a vulnerability gate
- Status: proposed · Date: 2026-11-19 · Area: delivery / supply chain (Docker, NuGet, CI tools) · Priority: P2

## Problem statement
The GitHub Actions are pinned well: all `uses:` lines in `.github/workflows/*.yml` and `.github/actions/fake-stack/action.yml`
carry a 40-character SHA, with the local action as the only exception. Python (`uv.lock`, `--frozen`) and npm (`package-lock.json`,
`npm ci`) are locked. The remaining gaps:

1. **Base images use floating tags.** `docker/api.Dockerfile:5-6` (`dotnet/sdk:10.0`, `aspnet:10.0`), `docker/web.Dockerfile:11-13`
   (`node:22-slim`, `sdk:10.0`, `aspnet:10.0`), `docker/worker.Dockerfile:32-35` (`python:3.12-slim`, `uv:0.13.5`,
   `deno:bin-2.9.8`), `docker/generator.Dockerfile:25-26`. Only the emsdk image is pinned by digest (`web.Dockerfile:16`). The same
   commit can build different images on different days, and `global.json:3-4` (`10.0.401`, `rollForward: latestFeature`) does not
   bind the SDK inside the image.
2. **Some packages get past the lock.** The GPU variant installs `torch`, `torchaudio`, `onnxruntime-gpu` and
   `cuda-toolkit[curand]` by version only, with no hashes, from an extra index (`worker.Dockerfile:59-62, 70-72`). The font stage
   installs `fonttools[woff]==4.68.0` without a hash (`:89`). In CI, 15 tool invocations resolve packages at run time, pinned by
   version without integrity: `npx --yes "$REDOCLY_CLI"` (`ci.yml:318-358`), `npx --yes license-checker@25.0.1` (`:617-621`),
   `dotnet dnx nuget-license@4.0.18` (`:603`), `--with pip-licenses==5.5.5` (`:608,613`), `--with "$PYYAML"` and friends (`:364-391`).
3. **NuGet has no lock.** `git ls-files | grep -c packages.lock.json` returns 0. There is no `nuget.config` with package source
   mapping, and `Directory.Packages.props:7` sets `CentralPackageTransitivePinningEnabled=false`. Transitive NuGet versions can
   change between restores. NuGet is the only ecosystem in the repository without a lock.
4. **CI has no vulnerability gate.** Every `npm ci` runs with `--no-audit` (for example `ci.yml:266, 293, 506`). There is no
   `dotnet list package --vulnerable` and no `pip-audit`. Sample: `npm audit --package-lock-only` in `tools/docs` finds 1 high,
   `markdown-it` 15.0.0–15.0.2, quadratic-time DoS, fixed in 15.0.3 (output in
   `.claude/scratchpad/main/npm-audit-tools-docs.json`). The input is repository Markdown, so it is hard to exploit, but it shows
   that nothing reports such findings.
5. **Dependabot coverage has gaps.** `.github/dependabot.yml:10` lists the uv folders `/worker`, `/tools/quality` and
   `/tests/fixtures`, but not `/generator` (`generator/uv.lock` exists, `ci.yml:232`). The docker entry (`:19-22`) reads `/docker`,
   but every `FROM` there is `FROM ${ARG}`. Check in the Dependabot log whether the ARG defaults get updated at all: Dependabot's
   docker updater is documented for literal `FROM image:tag` lines.
6. **Checkout keeps its credentials.** All 21 `actions/checkout` steps keep the default `persist-credentials: true` (grep: 0
   occurrences of `persist-credentials`), so the job token stays in `.git/config` for every later step, including the
   third-party tools above. The impact is limited by `permissions: contents: read` (`ci.yml:18-19`), but it is unnecessary.
7. **The secret scan has a narrow scope.** `scripts/secret_scan.py` scans tracked files for Claude keys only (run today: "5432
   files clean"). Other secret types and the git history are not scanned.

## Impact
- Reproducibility: an upstream re-tag (for example `python:3.12-slim` moving to a new Debian point release) can break or change an
  image without any commit. Bisecting such breakage is expensive.
- Security: an unpinned image or a tool fetched at run time is the classic entry point of a supply-chain attack. Vulnerable
  dependencies stay unseen until someone looks by hand.

## Risk
- Leaving it: low probability, high impact. The repository is public and the images run on users' machines.
- The change: low. Digest pins and lock files need regular updates; Dependabot (with the 14-day cooldown already set) does that.
  NuGet locked mode can fail a restore after an SDK update. Mitigation: refresh the lock in the same PR. Rollback: drop
  `RestoreLockedMode`.

## Effort
M (2–3 days). Assumes Dependabot stays the update mechanism. The vulnerability gate starts as a report and becomes blocking for
high/critical after one clean week.

## Step-by-step plan
1. Safety net: record the current image digests (`docker image inspect --format '{{.RepoDigests}}'`) and image sizes, so a changed
   pin shows up as a diff.
2. Pin every base image as `image:tag@sha256:...` in the `ARG` defaults. Confirm that Dependabot proposes digest updates; if it
   does not, move the defaults to literal `FROM` lines or add a small Renovate config. Check: the `cpu-image-guard` and
   `generator-image` jobs stay green.
3. NuGet: set `RestorePackagesWithLockFile=true` in `Directory.Build.props`, commit the generated `packages.lock.json` files, and
   restore with `--locked-mode` in CI and in the Dockerfiles. Add a `nuget.config` with `<packageSourceMapping>` for nuget.org.
4. Vulnerability gate: add one CI job that runs `npm audit --package-lock-only --audit-level=high` per package folder,
   `dotnet list package --vulnerable --include-transitive` (fail on High/Critical), and `uv run --with pip-audit pip-audit` per
   uv project. Report-only for one week, then blocking. Bump `markdown-it` in `tools/docs` to at least 15.0.3.
5. Move the run-time tools into lock files: redocly and license-checker as devDependencies of a small `tools/ci/package.json`
   with a lock (or of `tools/docs`). Pin pip-licenses, PyYAML, jsonschema and pytest in `tools/quality/uv.lock`, and nuget-license
   in `dotnet-tools.json`.
6. GPU wheels: generate a hashed requirements file for the cu132 set (`uv pip compile --generate-hashes`) and install with
   `--require-hashes`. Do the same for fonttools.
7. Add `persist-credentials: false` to every checkout, except the Sonar job if it needs git blame (it only needs the history, not
   the credentials).
8. Add `/generator` to the uv directories in `dependabot.yml`. Optional: a general secret scanner (for example gitleaks, pinned by
   SHA) over the full history next to `secret_scan.py`.
9. Verify: two builds of the same commit on different days produce the same image digest for api and web (worker, given apt, at
   least the same Python packages), and the vulnerability job is green. Roll back per step by reverting it.

## Dependencies and related proposals
- `004-loopback-ports.md` (runtime exposure of the same containers).
- `002-project-norm.md` (where the supply-chain rules are written down).
