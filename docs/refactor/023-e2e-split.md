# 023 Split the 100-minute E2E job and build the images once per run
- Status: proposed · Date: 2026-11-19 · Area: delivery / CI speed · Priority: P2

## Problem statement
Evidence comes from `gh run view 44241813956` (2026-11-18) and the job durations of the last 30 runs
(`.claude/scratchpad/main/ci-history.tsv`):

- **One serial job does all browser work.** "E2E main flow" (`ci.yml:627-776`) took 41–101 min across the last 30 runs, and the
  time keeps growing per feature (about 40 min on 2026-11-16, 101 min on 2026-11-18). Its step "Playwright (retries 0 ...)" alone
  took 84 min, because `e2e/playwright.config.ts:29-30` sets `workers: 1, fullyParallel: false`.
- **The first failure hides the rest.** In that run, "Play-along recording specs" failed, so all 8 later suites were `skipped`:
  practice-flow no-detect, restart, agent fake/none, lyric fake/none, gpu, gpu-oom. Nobody learns whether they pass.
- **E2E waits for everything.** `needs:` at `ci.yml:629` lists 10 jobs, so E2E starts only after the slowest unit job (`.NET`,
  15 min). Wall time on `main` is about 2 h (05:05 → 07:02).
- **The same images are built about 6 times per run, without a shared layer cache.** `docker compose up --build` runs in `e2e`
  (`ci.yml:680`), `visual`, `a11y` and `lighthouse` (through `.github/actions/fake-stack/action.yml:59`), plus the worker image again
  in `cpu-image-guard` (`ci.yml:520`) and the generator in `generator-image` (`ci.yml:572`). The BuildKit cache mounts in the
  Dockerfiles only live inside one runner. "fake-stack" alone took about 4 min in `lighthouse`.
- **The setup is copied and slow.** The fixture generation and backups-folder setup of `ci.yml:654-678` is a copy of
  `fake-stack/action.yml:30-53`. `e2e-real.yml:96-104` has a third variant that differs from both: it runs only
  `generate_fixtures.py`, and uses `chown` where the others use `install -d -m 2775`. `apt-get install ffmpeg` appears 8 times, and
  in run 44241813956 it took 6 min 50 s (`ci.yml:652-653`).

## Impact
- Feedback on a change takes about 2 h, which hits every round and every overnight cycle directly.
- A failure in one suite masks failures in up to 8 others, so fixing takes several 2-hour iterations.
- Runner minutes: about 6 image builds plus a 100-minute job per push.

## Risk
- Leaving it: E2E time grows with every feature (15 features so far). The 150-minute job timeout (`ci.yml:631`) is in reach.
- The change: medium. Parallel jobs mean parallel stacks, each with its own runner, so there is no shared state between suites.
  Suites that today depend on order inside one stack (restart after the main flow) must stay together in one shard. Rollback:
  keep the old job behind `workflow_dispatch` for one week and revert if the shards are flaky.

## Effort
M (2–4 days). Assumes the suites that are separate npm scripts today can run on separate fresh stacks. That holds for
recording, practice-flow-no-detect, agent, lyric and gpu, because each one already recreates the api or worker. Measure it first.

## Step-by-step plan
1. Safety net: record the per-step durations and the pass/fail per suite of three consecutive runs as the baseline.
2. Add an `images` job: `docker buildx bake` (or `docker/build-push-action`, pinned by SHA) for worker, api and web, with
   `cache-from/cache-to: type=gha`. Export them with `docker save` as one artifact (no registry push, no secrets). `cpu-image-guard`,
   `generator-image` and every E2E job `docker load` it instead of building. Check: only one job runs `docker build` per run.
3. Extend `fake-stack` with a `load-images` input and use it in the `e2e` job as well. Delete the copied steps `ci.yml:652-678`.
   Make `e2e-real.yml` use the same action for the fixtures and the backups folder, with a `separator` input.
4. Turn the E2E job into a `strategy.matrix` over suite groups (main flow + restart; recording; practice no-detect; agent;
   lyric; gpu + gpu-oom), with `fail-fast: false`. Each shard runs on its own runner with its own stack. Split the main flow
   further with Playwright `--shard=i/n`, if its specs do not share data.
5. Narrow `needs:` to what E2E really needs (`images`, `e2e-lint`). The unit jobs then run in parallel and stay required through
   `ci-ok`.
6. ffmpeg: install it once into a cached location, or into an image the E2E jobs already pull. Measure first whether the
   `ubuntu-24.04` runner image already provides it.
7. Verify: wall time and summed runner minutes against the baseline. Target: E2E under 30 min wall time, and each suite reports
   its own verdict. Roll back by reverting the workflow commit.

## Dependencies and related proposals
- `008-protect-main.md` (the `ci-ok` aggregate job collects the matrix results).
- `001-dotnet-affected.md` covers the .NET part of CI speed. This proposal covers the container and browser part and does not
  overlap with it.
