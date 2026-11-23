# Refactor proposals
| # | Title | Area | Priority | Effort | Risk | Status |
|---|---|---|---|---|---|---|
| [001](001-dotnet-affected.md) | Affected builds and tests for the .NET monorepo | build / CI | P2 | S | low | proposed |
| [002](002-project-norm.md) | Project norm map | norm / governance | P2 | S | low | proposed |
| [003](003-api-exposure.md) | Close the unauthenticated API to callers other than the Web app | Api / security | P1 | M | medium | done (2026-11-23) |
| [004](004-loopback-ports.md) | Publish the web and api ports on localhost only by default | docker compose / security | P1 | S | low | done (2026-11-23) |
| [005](005-worker-ffmpeg-input-hardening.md) | Harden the worker's ffmpeg/ffprobe intake against disguised playlists | worker / ingest security | P1 | S | medium | proposed |
| [006](006-stale-track-writes.md) | Optimistic concurrency for Track and TrackLyrics instead of per-feature locks | Api sync / Persistence | P1 | M | medium | proposed |
| [007](007-quality-gates-real.md) | Make the SonarQube and Sigrid gates real and blocking | CI / quality gates | P1 | S-M | low | rejected (ADR-038) |
| [008](008-protect-main.md) | Protect main and require a green CI before code lands | repository policy | P1 | S | low-medium | proposed |
| [009](009-instrument-catalogue.md) | One instrument family catalogue for Domain, Contracts and Api | Domain / Contracts / Api | P2 | M | low | proposed |
| [010](010-mixer-engine-lifetime.md) | One owner for the audio engine across Mixer instances | Web mixer / audio interop | P2 | M | medium | proposed |
| [011](011-gpu-coordination-policy.md) | Align the cross-container GPU lease and memory policy | worker + generator / devices | P2 | M | medium | proposed |
| [012](012-e2e-tags-never-run.md) | E2E specs that no pipeline runs, and a hand-maintained tag matrix | e2e Playwright | P2 | S | low | proposed |
| [013](013-gate-gaps.md) | Close the silent holes in the 90 % coverage and E2E gates | CI / gate correctness | P2 | S-M | low | proposed |
| [014](014-supply-chain-pinning.md) | Close the supply-chain gaps: image digests, NuGet lock files, a vulnerability gate | supply chain | P2 | M | low | proposed |
| [015](015-contained-paths.md) | Resolve every stored data-relative path through one containment check | Api / Storage, LibraryCare | P2 | S | low | proposed |
| [016](016-unify-job-lane-persistence.md) | Unify job persistence, restart reconciliation and delete semantics | worker / jobs | P2 | M-L | medium | proposed |
| [017](017-worker-client-segregation.md) | Split IWorkerClient into job-family ports | Api / worker client | P2 | M | low | proposed |
| [018](018-composition-modules.md) | Per-feature registration modules instead of one composition hotspot | Api / composition root | P2 | S | low | proposed |
| [019](019-mixer-component-split.md) | Split the Mixer page component into feature components | Web / Components/Mixer | P2 | L | medium | proposed |
| [020](020-split-worker-hub-modules.md) | Split the worker's ports.py and schemas.py hub modules per feature | worker / module structure | P2 | M | low | proposed |
| [021](021-shared-runtime-package.md) | Extract a shared runtime package for the worker and generator | worker + generator | P2 | M | medium | proposed |
| [022](022-single-source-checks.md) | One source for the checks that CI and test-all both run | scripts / CI | P2 | M | low-medium | proposed |
| [023](023-e2e-split.md) | Split the 100-minute E2E job and build the images once per run | CI speed | P2 | M | medium | proposed |
| [024](024-agent-step-pipeline.md) | Split the agent StepExecutor into explicit pipeline stages | Agent / runtime | P3 | M | medium | proposed |
| [025](025-transcription-profile-strategy.md) | Replace scattered score-profile conditionals with one profile strategy table | worker / transcription | P3 | S | low | proposed |
| [026](026-interop-lifetime-helper.md) | One JS-module and handle lifetime helper for all interop wrappers | Web / JS interop | P3 | S | low | proposed |
| [027](027-browser-glue-toolchain.md) | Bring the hand-written browser scripts into the TypeScript toolchain | wwwroot/js / Web.Tests | P3 | S | low | proposed |
| [028](028-breakpoint-tokens.md) | One set of breakpoints from the design spec, in one unit | wwwroot/css | P3 | S | low | proposed |
| [029](029-repo-hygiene.md) | Repository hygiene: generated output in git, binary growth, ignore-file drift | repository hygiene | P3 | S | low | proposed |
| [030](030-adr-038-no-sonarqube-sigrid.md) | ADR-038: No SonarQube or Sigrid for this project | CI / quality gates | P1 | S | low | accepted (ADR-038 placed) |
