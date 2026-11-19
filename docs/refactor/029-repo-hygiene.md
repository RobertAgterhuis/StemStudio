# 029 Repository hygiene: generated output in git, binary growth, ignore-file drift
- Status: proposed · Date: 2026-11-19 · Area: delivery / repository hygiene · Priority: P3

## Problem statement
1. **Binaries make up most of the history.** The pack is 128 MiB (`git count-objects -vH`). Measured with
   `git rev-list --objects --all -- <path> | git cat-file --batch-check`:

   | Path | Blobs | Size on disk |
   |---|---|---|
   | `e2e/visual/__screenshots__` | 799 | 85.6 MiB |
   | `docs/public` (2 PDFs of about 2 MB each, rebuilt in 22 commits, plus sources) | 221 | 18.5 MiB |
   | `docs/design-review` | 23 | 7.5 MiB |

   Together that is about 111 MiB of the 128 MiB. Every visual-baseline refresh adds several MiB that cannot be removed.
2. **A generated report is committed every round.** `docs/quality/SIG-REPORT.md` (660 KB, written by `tools/quality/run_all.py
   --report`, `report.py:57`) changed in 31 commits in the last 30 days. CI already produces the same report as an artifact
   (`ci.yml:476-490`). Delta compression keeps the disk cost small (0.3 MiB), but every lane rewrites the file, which causes merge
   conflicts and noisy diffs.
3. **The ignore files have drifted apart.** `.dockerignore` excludes `src/StemStudio.Web/wwwroot/third-party`, but no `.gitignore`
   covers it (`git check-ignore` on `wwwroot/third-party/soundtouch/x`: not ignored). Output of `scripts/build-soundtouch.sh
   --package` placed there locally would show up as untracked. The other way round, `src/StemStudio.Web/wwwroot/js/.gitignore`
   ignores `audio-engine-capture-worklet.js(.map)` (015), but `.dockerignore` does not exclude it. A stale local bundle could end
   up in the build context (the image build overwrites it, so this is harmless today).
4. **Evaluation logs are committed.** `tools/agent-eval/out/` holds 10 tracked files (`run1.log`..`run5.log` and the `.md` results,
   27 KB, commit `8a148fd9`). The files are small and were committed on purpose as evidence, but they mix run output into a source
   folder. Prefer `docs/` or a release artifact.
5. **Local state.** `.gitignore` has an uncommitted change (5 lines, the Tipoca entries). `git count-objects` reports 26.55 MiB of
   garbage and "no corresponding .idx" for one pack. That is local only, but it is worth a `git gc` by the user. Not run here, since
   this is audit mode.

## Impact
- Clone and CI checkout time grow with every visual-baseline update. There are 21 checkouts per CI run, all full content.
- Generated files in git cause merge conflicts between parallel lanes and hide real changes in review.
- Ignore drift can leak local build output into commits or images.

## Risk
- Leaving it: slow growth. Nothing breaks soon.
- The change: low for items 2–4. Moving the screenshots to Git LFS (item 1) changes the workflow of everyone who clones (LFS
  needed), and rewriting history is high-risk and not proposed. Only new versions would go to LFS. Rollback: `git lfs migrate
  export` for new files.

## Effort
S (0.5–1 day) for items 2–4. Item 1 is an S decision plus an S change, if the user chooses LFS for new baselines.

## Step-by-step plan
1. Safety net: record the clone size and the checkout time of the `dotnet` job as the baseline.
2. SIG report: stop committing `docs/quality/SIG-REPORT.md`. Keep the CI artifact, and publish a summary on the Pages site (the
   site already builds from the repository). Add the file to `.gitignore` and remove it from the index in one commit. Update
   `run_all.py`'s default and the round instructions that ask for the report to be regenerated.
3. Ignore drift: add `src/StemStudio.Web/wwwroot/third-party/` to `.gitignore`, and
   `src/StemStudio.Web/wwwroot/js/audio-engine-capture-worklet.js*` to `.dockerignore`. Add a small test in `scripts/tests` that every
   build-output path in the `wwwroot/js/.gitignore` is also in `.dockerignore`.
4. Move `tools/agent-eval/out/` to `docs/agent-eval/results/` (or attach it to a release), and ignore `tools/agent-eval/out/` from
   then on.
5. Binaries (the user decides): track `e2e/visual/__screenshots__/**/*.png` and `docs/public/*.pdf` with Git LFS from now on,
   with `lfs: true` on the checkouts that need them (visual, pages). Or publish the PDFs only through Pages and releases.
6. Commit the pending `.gitignore` change, and run `git gc` locally (the user's action).
7. Verify: `git status` stays clean after a full local build plus `build-soundtouch --package`, and the clone size of new clones
   stops growing with baseline updates. Roll back by reverting the ignore changes.

## Dependencies and related proposals
- `013-gate-gaps.md` (the ignore-consistency test lives in `scripts/tests` under the same gate).
